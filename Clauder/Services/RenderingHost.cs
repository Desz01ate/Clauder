namespace Clauder.Services;

using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;
using Clauder.Pages;
using Concur;
using Spectre.Console;
using Spectre.Console.Rendering;
using static Concur.ConcurRoutine;

public sealed class RenderingHost : IDisposable
{
    private readonly ChannelReader<NavigationCommand> _navigationReader;
    private readonly IPageFactory _pageFactory;
    private readonly INavigationContext _navigationContext;
    private readonly Stack<IPage> _pageStack = new();
    private CancellationTokenSource? _currentPageCancellation;
    private string? _lastErrorMessage;
    private DateTime? _lastErrorTime;

    private static IRenderable EmptyErrorDisplay => new Markup(string.Empty);

    private static Layout Layout =
        new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content"),
                new Layout("Footer")
                    .Size(3)
                    .SplitColumns(
                        new Layout("FooterMain").Ratio(80),
                        new Layout("FooterError").Ratio(20)
                    )
            );

    public RenderingHost(
        ChannelReader<NavigationCommand> navigationReader,
        IPageFactory pageFactory,
        INavigationContext navigationContext)
    {
        this._navigationReader = navigationReader;
        this._pageFactory = pageFactory;
        this._navigationContext = navigationContext;
    }

    public async Task RunAsync()
    {
        // Start with the initial page
        await this.HandleNavigateToCommand(new NavigateToCommand(typeof(ProjectsPage), []));

        var wg = new WaitGroup();

        var cts = new CancellationTokenSource();

        Go(wg, () => this.RunRenderingLoopAsync(cts));
        Go(wg, () => this.ProcessNavigationCommandsAsync(cts));

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
                             try
                             {
                                 await page.InitializeAsync(cancellationToken);
                             }
                             catch (Exception ex)
                             {
                                 // Log initialization error but allow rendering to continue
                                 System.Diagnostics.Debug.WriteLine($"Page initialization failed: {ex.Message}");
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

                                         // Update error state for display
                                         this._lastErrorMessage = $"Input: {ex.Message}";
                                         this._lastErrorTime = DateTime.Now;
                                     }
                                 }

                                 // Always re-render after input to reflect any state changes
                                 await this.UpdateLayoutAsync(page);

                                 ctx.Refresh();
                             }
                         });
    }

    private async Task UpdateLayoutAsync(IPage page)
    {
        var header =
            await this.SafeRenderFragmentAsync(
                page.RenderHeaderAsync,
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
        Layout["FooterError"].Update(this.CreateErrorDisplay());
    }

    private async Task<IRenderable> SafeRenderFragmentAsync(
        Func<ValueTask<IRenderable>> renderFunc,
        string fragmentName,
        Func<IRenderable> fallbackRenderer)
    {
        try
        {
            return await renderFunc();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fragment rendering failed ({fragmentName}): {ex.Message}");

            // Update error state for display
            this._lastErrorMessage = $"{fragmentName}: {ex.Message}";
            this._lastErrorTime = DateTime.Now;

            return fallbackRenderer();
        }
    }

    private Task HandleRenderingErrorAsync(IPage currentPage, Exception ex)
    {
        // Log the error
        System.Diagnostics.Debug.WriteLine($"Rendering error for page {currentPage.GetType().Name}: {ex.Message}");

        // Update error state for footer display instead of blocking modal
        this._lastErrorMessage = $"Page: {ex.Message}";
        this._lastErrorTime = DateTime.Now;

        return Task.CompletedTask;
    }

    private IRenderable CreateErrorDisplay()
    {
        if (this._lastErrorMessage == null || this._lastErrorTime == null)
        {
            return EmptyErrorDisplay;
        }

        // Auto-clear errors after 5 seconds
        var timeSinceError = DateTime.Now - this._lastErrorTime.Value;

        if (timeSinceError.TotalSeconds > 5)
        {
            this._lastErrorMessage = null;
            this._lastErrorTime = null;

            return EmptyErrorDisplay;
        }

        var errorText = this._lastErrorMessage.Length > 55
            ? this._lastErrorMessage[..52] + "..."
            : this._lastErrorMessage;

        return new Markup($"[red]⚠ {errorText}[/]\n[dim][/]");
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
                    this.HandleNavigateBackCommand();
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

    private async Task HandleNavigateToCommand(NavigateToCommand command)
    {
        var page = this._pageFactory.CreatePage(command.PageType, command.Args);

        // Cancel current page display to allow navigation
        await (this._currentPageCancellation?.CancelAsync() ?? Task.CompletedTask);

        // Clear any error state when navigating
        this._lastErrorMessage = null;
        this._lastErrorTime = null;

        this._pageStack.Push(page);

        // Give the rendering loop a moment to pick up the new page
        await Task.Yield();
    }

    private void HandleNavigateBackCommand()
    {
        // Cancel current page display to allow navigation
        this._currentPageCancellation?.Cancel();

        // Clear any error state when navigating back
        this._lastErrorMessage = null;
        this._lastErrorTime = null;

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

        while (this._pageStack.Count > 0)
        {
            var page = this._pageStack.Pop();
            page.Dispose();
        }
    }
}