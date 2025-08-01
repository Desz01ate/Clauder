namespace Clauder.Services;

using Clauder.Abstractions;

public sealed class NavigationService : INavigationService, IDisposable
{
    private readonly Stack<IDisplay> _pageStack = new();
    private bool _shouldExit = false;

    public bool HasPages => this._pageStack.Count > 0;
    public bool ShouldExit => this._shouldExit;
    public string CurrentTitle => this._pageStack.TryPeek(out var currentPage) ? currentPage.Title : "[#CC785C]Clauder[/]";

    public Task NavigateToAsync<T>(T page) where T : IDisplay
    {
        this._pageStack.Push(page);
        return Task.CompletedTask;
    }

    public Task NavigateBackAsync()
    {
        if (this._pageStack.Count > 0)
        {
            var currentPage = this._pageStack.Pop();
            currentPage.Dispose();
        }
        return Task.CompletedTask;
    }

    public Task ExitAsync()
    {
        this._shouldExit = true;
        return Task.CompletedTask;
    }

    public async Task RunAsync()
    {
        while (this.HasPages && !this.ShouldExit)
        {
            var currentPage = this._pageStack.Peek();
            await currentPage.DisplayAsync();
        }
    }

    public void Dispose()
    {
        while (this._pageStack.Count > 0)
        {
            var page = this._pageStack.Pop();
            page.Dispose();
        }
    }
}