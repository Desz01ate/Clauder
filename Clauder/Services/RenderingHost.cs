namespace Clauder.Services;

using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;
using Clauder.Pages;
using Concur;
using Spectre.Console;
using Spectre.Console.Rendering;
using static Concur.ConcurRoutine;

internal record struct ToastInfo(string Message, DateTime Time, ToastType Type, TimeSpan Duration);

public sealed class RenderingHost : IDisposable
{
    private readonly ChannelReader<NavigationCommand> _navigationReader;
    private readonly ChannelReader<ToastCommand> _toastReader;
    private readonly IPageFactory _pageFactory;
    private readonly INavigationContext _navigationContext;
    private readonly IToastContext _toastContext;
    private readonly Stack<IPage> _pageStack = new();
    private CancellationTokenSource? _currentPageCancellation;

    #region Toast handling

    private readonly Queue<ToastInfo> _toastQueue = new();
    private ToastInfo? _currentToast;
    private CancellationTokenSource? _currentToastCancellation;
    private LiveDisplayContext? _liveContext;

    private readonly object _renderLock = new();
    private readonly object _toastLock = new();

    #endregion

    private static IRenderable EmptyErrorDisplay => new Markup(string.Empty);

    private static Layout Layout =
        new Layout("Root")
            .SplitRows(
                new Layout("Header")
                    .Size(4),
                new Layout("Content"),
                new Layout("Footer")
                    .Size(4)
                    .SplitColumns(
                        new Layout("FooterMain").Ratio(70),
                        new Layout("FooterError").Ratio(30)
                    )
            );

    public RenderingHost(
        ChannelReader<NavigationCommand> navigationReader,
        ChannelReader<ToastCommand> toastReader,
        IPageFactory pageFactory,
        INavigationContext navigationContext,
        IToastContext toastContext)
    {
        this._navigationReader = navigationReader;
        this._toastReader = toastReader;
        this._pageFactory = pageFactory;
        this._navigationContext = navigationContext;
        this._toastContext = toastContext;
    }

    public async Task RunAsync()
    {
        // Start with the initial page
        await this.HandleNavigateToCommand(new NavigateToCommand(typeof(ProjectsPage), []));

        var wg = new WaitGroup();

        var cts = new CancellationTokenSource();

        Go(wg, () => this.RunRenderingLoopAsync(cts));
        Go(wg, () => this.ProcessNavigationCommandsAsync(cts));
        Go(wg, () => this.ProcessToastCommandsAsync(cts));
        Go(wg, () => this.HandleToastAsync(cts));

        await wg.WaitAsync();
    }

    private async Task RunRenderingLoopAsync(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;

        while (!cancellationToken.IsCancellationRequested && this._pageStack.Count > 0)
        {
            var currentPage = this._pageStack.Peek();

            this._currentPageCancellation?.Dispose();
            this._currentPageCancellation = new CancellationTokenSource();

            try
            {
                await this.RenderPageAsync(currentPage, this._currentPageCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Navigation occurred, continue the loop
                continue;
            }
            catch (Exception ex)
            {
                // Try to recover from the error rather than breaking completely
                try
                {
                    await this.HandleRenderingErrorAsync(currentPage, ex);

                    // Brief pause before retrying
                    await Task.Delay(1000, cancellationToken);
                }
                catch
                {
                    // If recovery fails, show error and break
                    AnsiConsole.WriteException(ex);

                    break;
                }
            }
        }

        await cts.CancelAsync();
    }

    private async Task RenderPageAsync(IPage page, CancellationToken cancellationToken)
    {
        Console.Title = page.Title;

        await AnsiConsole.Live(Layout)
                         .StartAsync(async ctx =>
                         {
                             // Store the live context for independent refreshes
                             lock (this._renderLock)
                             {
                                 this._liveContext = ctx;
                             }

                             try
                             {
                                 await page.InitializeAsync(cancellationToken);
                             }
                             catch (Exception ex)
                             {
                                 // Log initialization error but allow rendering to continue
                                 System.Diagnostics.Debug.WriteLine($"Page initialization failed: {ex.Message}");

                                 await this._toastContext.ShowErrorAsync(ex.Message);
                             }

                             await this.UpdateLayoutAsync(page);

                             ctx.Refresh();

                             while (!cancellationToken.IsCancellationRequested)
                             {
                                 var keyInfo = Console.ReadKey(true);

                                 // Handle common navigation keys globally
                                 var handled = await this.HandleGlobalInputAsync(keyInfo);

                                 // If not handled globally, let the page handle it
                                 if (!handled && page is IInputHandler inputHandler)
                                 {
                                     try
                                     {
                                         await inputHandler.HandleInputAsync(keyInfo, cancellationToken);
                                     }
                                     catch (Exception ex)
                                     {
                                         // Log input handling errors but don't crash the application
                                         System.Diagnostics.Debug.WriteLine($"Input handling failed: {ex.Message}");

                                         await this._toastContext.ShowErrorAsync(ex.Message);
                                     }
                                 }

                                 // Always re-render after input to reflect any state changes
                                 await this.UpdateLayoutAsync(page);

                                 ctx.Refresh();
                             }

                             // Clear the live context when rendering ends
                             lock (this._renderLock)
                             {
                                 this._liveContext = null;
                             }
                         });
    }

    private async Task UpdateLayoutAsync(IPage page)
    {
        var header =
            await this.SafeRenderFragmentAsync(
                async () =>
                {
                    var content = await page.RenderHeaderAsync();
                    var padder = new Padder(
                        content,
                        new Padding(0, 1, 0, 0));

                    return padder;
                },
                "Header",
                static () => new Markup("[red]Header rendering failed[/]"));

        var body =
            await this.SafeRenderFragmentAsync(
                page.RenderBodyAsync,
                "Body",
                static () => new Markup("[red]Content rendering failed[/]"));

        var footer =
            await this.SafeRenderFragmentAsync(
                page.RenderFooterAsync,
                "Footer",
                static () => new Markup("[red]Footer rendering failed[/]"));

        Layout["Header"].Update(header);
        Layout["Content"].Update(body);
        Layout["FooterMain"].Update(footer);
    }

    private void RefreshRender()
    {
        lock (this._renderLock)
        {
            this._liveContext?.Refresh();
        }
    }

    private async Task<IRenderable> SafeRenderFragmentAsync(
        Func<ValueTask<IRenderable>> renderFunc,
        string fragmentName,
        Func<IRenderable> fallbackRenderer)
    {
        try
        {
            var content = await renderFunc();

            return content;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fragment rendering failed ({fragmentName}): {ex.Message}");

            await this._toastContext.ShowErrorAsync(ex.Message);

            return fallbackRenderer();
        }
    }

    private async Task HandleRenderingErrorAsync(IPage currentPage, Exception ex)
    {
        // Log the error
        System.Diagnostics.Debug.WriteLine($"Rendering error for page {currentPage.GetType().Name}: {ex.Message}");

        await this._toastContext.ShowErrorAsync(ex.Message);
    }

    private async Task HandleToastAsync(CancellationTokenSource cts)
    {
        while (!cts.IsCancellationRequested)
        {
            ToastInfo? nextToast = null;

            // Check if we need to start a new toast
            lock (this._toastLock)
            {
                if (this._currentToast == null && this._toastQueue.Count > 0)
                {
                    nextToast = this._toastQueue.Dequeue();
                    this._currentToast = nextToast;
                }
            }

            // Start displaying the new toast
            if (nextToast.HasValue)
            {
                this._currentToastCancellation?.Cancel();
                this._currentToastCancellation = new CancellationTokenSource();

                var toastToken = this._currentToastCancellation.Token;

                // Display the toast
                this.DisplayToast(nextToast.Value);

                try
                {
                    // Wait for the duration or cancellation
                    await Task.Delay(nextToast.Value.Duration, toastToken);

                    // Duration completed, clear this toast
                    lock (this._toastLock)
                    {
                        if (this._currentToast?.Time == nextToast.Value.Time)
                        {
                            this._currentToast = null;
                            this.ClearToastDisplay();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Toast was cancelled (new toast arrived or system shutdown)
                    // Don't clear display here as new toast might be showing
                }
            }
            else if (this._currentToast == null)
            {
                // No current toast, ensure display is clear
                this.ClearToastDisplay();
            }

            await Task.Delay(50, cts.Token);
        }
    }

    private void DisplayToast(ToastInfo toast)
    {
        var toastText = toast.Message.Length > 55
            ? toast.Message[..52] + "..."
            : toast.Message;

        var (icon, color) = toast.Type switch
        {
            ToastType.Success => ("✓", "green"),
            ToastType.Warning => ("⚠", "yellow"),
            ToastType.Error => ("✗", "red"),
            ToastType.Info or _ => ("ℹ", "blue"),
        };

        var panel = new Panel($"[{color}]{icon} {toastText}[/]")
                    .RoundedBorder()
                    .BorderColor(Color.FromInt32(toast.Type switch
                    {
                        ToastType.Success => 28, // Green
                        ToastType.Warning => 178, // Yellow/Orange
                        ToastType.Error => 196, // Red
                        ToastType.Info or _ => 33, // Blue
                    }))
                    .Padding(0, 0);

        Layout["FooterError"].Update(panel);
        this.RefreshRender();
    }

    private void ClearToastDisplay()
    {
        Layout["FooterError"].Update(EmptyErrorDisplay);
        this.RefreshRender();
    }

    private async Task<bool> HandleGlobalInputAsync(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.B:
                await this._navigationContext.NavigateBackAsync();
                return true;

            case ConsoleKey.O:
                await this._navigationContext.NavigateToAsync<SettingsPage>();
                return true;

            default:
                return false;
        }
    }

    private async Task ProcessNavigationCommandsAsync(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;

        await foreach (var command in this._navigationReader.ReadAllAsync(cancellationToken))
        {
            switch (command)
            {
                case NavigateToCommand navigateCommand:
                    await this.HandleNavigateToCommand(navigateCommand);
                    break;

                case NavigateBackCommand:
                    await this.HandleNavigateBackCommand();
                    break;

                case ExitCommand:
                    return; // Exit the processing loop

                default:
                    AnsiConsole.MarkupLine($"[red]Unknown navigation command: {command.GetType().Name}[/]");
                    break;
            }
        }

        await cts.CancelAsync();
    }

    private async Task ProcessToastCommandsAsync(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;

        await foreach (var command in this._toastReader.ReadAllAsync(cancellationToken))
        {
            switch (command)
            {
                case ShowToastCommand showCommand:
                    var toastInfo = new ToastInfo(
                        showCommand.Message,
                        DateTime.Now,
                        showCommand.Type,
                        showCommand.Duration);

                    lock (this._toastLock)
                    {
                        this._toastQueue.Enqueue(toastInfo);
                    }

                    break;

                case ClearToastCommand:
                    lock (this._toastLock)
                    {
                        // Clear the queue and current toast
                        this._toastQueue.Clear();
                        this._currentToast = null;
                    }

                    // Cancel current toast display
                    this._currentToastCancellation?.Cancel();
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine($"Unknown toast command: {command.GetType().Name}");

                    await this._toastContext.ShowErrorAsync($"Unknown toast command: {command.GetType().Name}");

                    break;
            }
        }

        await cts.CancelAsync();
    }

    private async Task HandleNavigateToCommand(NavigateToCommand command)
    {
        var page = this._pageFactory.CreatePage(command.PageType, command.Args);

        // Cancel current page display to allow navigation
        await (this._currentPageCancellation?.CancelAsync() ?? Task.CompletedTask);

        await this._toastContext.ClearAsync();

        this._pageStack.Push(page);

        // Give the rendering loop a moment to pick up the new page
        await Task.Yield();
    }

    private async Task HandleNavigateBackCommand()
    {
        // Cancel current page display to allow navigation
        this._currentPageCancellation?.Cancel();

        await this._toastContext.ClearAsync();

        if (this._pageStack.Count > 0)
        {
            var currentPage = this._pageStack.Pop();
            currentPage.Dispose();
        }
    }

    public void Dispose()
    {
        this._currentPageCancellation?.Cancel();
        this._currentPageCancellation?.Dispose();

        this._currentToastCancellation?.Cancel();
        this._currentToastCancellation?.Dispose();

        while (this._pageStack.Count > 0)
        {
            var page = this._pageStack.Pop();
            page.Dispose();
        }
    }
}