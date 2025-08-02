namespace Clauder.Services;

using Clauder.Abstractions;
using Clauder.Models;

public sealed class PageManager : IPageManager
{
    private readonly IPageFactory _pageFactory;
    private readonly Stack<IPage> _pageStack = new();
    private readonly object _stackLock = new();
    private bool _disposed;

    public PageManager(IPageFactory pageFactory)
    {
        this._pageFactory = pageFactory;
    }

    public IPage? CurrentPage
    {
        get
        {
            lock (this._stackLock)
            {
                return this._pageStack.Count > 0 ? this._pageStack.Peek() : null;
            }
        }
    }

    public bool HasPages
    {
        get
        {
            lock (this._stackLock)
            {
                return this._pageStack.Count > 0;
            }
        }
    }

    public event EventHandler<PageChangedEventArgs>? PageChanged;

    public Task NavigateToAsync(NavigateToCommand command, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        var newPage = this._pageFactory.CreatePage(command.PageType, command.Args);
        IPage? previousPage;

        lock (this._stackLock)
        {
            previousPage = this._pageStack.Count > 0 ? this._pageStack.Peek() : null;
            this._pageStack.Push(newPage);
        }

        this.OnPageChanged(previousPage, newPage);

        return Task.CompletedTask;
    }

    public Task NavigateBackAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        IPage? previousPage = null;
        IPage? newPage = null;

        lock (this._stackLock)
        {
            if (this._pageStack.Count > 0)
            {
                previousPage = this._pageStack.Pop();
                newPage = this._pageStack.Count > 0 ? this._pageStack.Peek() : null;
            }
        }

        if (previousPage != null)
        {
            try
            {
                previousPage.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }

            this.OnPageChanged(previousPage, newPage);
        }

        return Task.CompletedTask;
    }

    public Task ClearStackAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        var pagesToDispose = new List<IPage>();

        lock (this._stackLock)
        {
            while (this._pageStack.Count > 0)
            {
                pagesToDispose.Add(this._pageStack.Pop());
            }
        }

        foreach (var page in pagesToDispose)
        {
            try
            {
                page.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        this.OnPageChanged(pagesToDispose.FirstOrDefault(), null);

        return Task.CompletedTask;
    }

    private void OnPageChanged(IPage? previousPage, IPage? newPage)
    {
        this.PageChanged?.Invoke(this, new PageChangedEventArgs(previousPage, newPage));
    }

    private void ThrowIfDisposed()
    {
        if (this._disposed)
            throw new ObjectDisposedException(nameof(PageManager));
    }

    public void Dispose()
    {
        if (this._disposed)
            return;

        this._disposed = true;

        lock (this._stackLock)
        {
            while (this._pageStack.Count > 0)
            {
                var page = this._pageStack.Pop();

                try
                {
                    page.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }
}