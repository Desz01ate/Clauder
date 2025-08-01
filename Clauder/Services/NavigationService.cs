namespace Clauder.Services;

using Clauder.Abstractions;
using Spectre.Console;

public sealed class NavigationService : INavigationService, IDisposable
{
    private readonly Stack<IDisplay> _pageStack = new();
    private bool _shouldExit = false;
    private CancellationTokenSource? _currentPageCancellation;

    public bool HasPages => this._pageStack.Count > 0;

    public bool ShouldExit => this._shouldExit;

    public string CurrentTitle => this._pageStack.TryPeek(out var currentPage) ? currentPage.Title : "[#CC785C]Clauder[/]";

    public Task NavigateToAsync<T>(T page) where T : IDisplay
    {
        // Cancel current page display to allow navigation
        this._currentPageCancellation?.Cancel();

        this._pageStack.Push(page);

        return Task.CompletedTask;
    }

    public Task NavigateBackAsync()
    {
        // Cancel current page display to allow navigation
        this._currentPageCancellation?.Cancel();

        if (this._pageStack.Count > 0)
        {
            var currentPage = this._pageStack.Pop();
            currentPage.Dispose();
        }

        return Task.CompletedTask;
    }

    public Task ExitAsync()
    {
        this._currentPageCancellation?.Cancel();
        this._shouldExit = true;
        return Task.CompletedTask;
    }

    public async Task RunAsync()
    {
        while (this.HasPages && !this.ShouldExit)
        {
            var currentPage = this._pageStack.Peek();

            this._currentPageCancellation?.Dispose();
            this._currentPageCancellation = new CancellationTokenSource();

            try
            {
                await currentPage.DisplayAsync(this._currentPageCancellation.Token);
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