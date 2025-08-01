namespace Clauder.Services;

using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;
using Clauder.Pages;
using Concur;
using Spectre.Console;
using static Concur.ConcurRoutine;

public sealed class RenderingHost : IDisposable
{
    private readonly ChannelReader<NavigationCommand> _navigationReader;
    private readonly IPageFactory _pageFactory;
    private readonly INavigationService _navigationService;
    private readonly Stack<IPage> _pageStack = new();
    private CancellationTokenSource? _currentPageCancellation;

    public RenderingHost(
        ChannelReader<NavigationCommand> navigationReader,
        IPageFactory pageFactory,
        INavigationService navigationService)
    {
        this._navigationReader = navigationReader;
        this._pageFactory = pageFactory;
        this._navigationService = navigationService;
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
                AnsiConsole.WriteException(ex);
                break;
            }
        }

        await cts.CancelAsync();
    }

    private async Task RenderPageAsync(IPage page, CancellationToken cancellationToken)
    {
        Console.Title = page.Title;

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content"),
                new Layout("Footer").Size(3)
            );

        await AnsiConsole.Live(layout)
                         .StartAsync(async ctx =>
                         {
                             await page.InitializeAsync(cancellationToken);
                             
                             await this.UpdateLayoutAsync(layout, page);

                             ctx.Refresh();

                             while (!cancellationToken.IsCancellationRequested)
                             {
                                 var keyInfo = Console.ReadKey(true);

                                 // Handle common navigation keys globally
                                 var handled = await this.HandleGlobalInputAsync(keyInfo);

                                 // If not handled globally, let the page handle it
                                 if (!handled && page is IInputHandler inputHandler)
                                 {
                                     await inputHandler.HandleInputAsync(keyInfo, cancellationToken);
                                 }

                                 // Always re-render after input to reflect any state changes
                                 await this.UpdateLayoutAsync(layout, page);

                                 ctx.Refresh();
                             }
                         });
    }

    private async Task UpdateLayoutAsync(Layout layout, IPage page)
    {
        var header = await page.RenderHeaderAsync();
        var body = await page.RenderBodyAsync();
        var footer = await page.RenderFooterAsync();

        layout["Header"].Update(header);
        layout["Content"].Update(body);
        layout["Footer"].Update(footer);
    }

    private async Task<bool> HandleGlobalInputAsync(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.B:
                await this._navigationService.NavigateBackAsync();
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

        this._pageStack.Push(page);

        // Give the rendering loop a moment to pick up the new page
        await Task.Yield();
    }

    private void HandleNavigateBackCommand()
    {
        // Cancel current page display to allow navigation
        this._currentPageCancellation?.Cancel();

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