namespace Clauder.Abstractions;

using Clauder.Models;
using Commands;

public interface IPageManager : IDisposable
{
    IPage? CurrentPage { get; }
    bool HasPages { get; }
    
    event EventHandler<PageChangedEventArgs>? PageChanged;
    
    Task NavigateToAsync(NavigateToCommand command, CancellationToken cancellationToken = default);
    Task NavigateBackAsync(CancellationToken cancellationToken = default);
    Task ClearStackAsync(CancellationToken cancellationToken = default);
}

public sealed class PageChangedEventArgs : EventArgs
{
    public IPage? PreviousPage { get; }
    public IPage? NewPage { get; }
    
    public PageChangedEventArgs(IPage? previousPage, IPage? newPage)
    {
        this.PreviousPage = previousPage;
        this.NewPage = newPage;
    }
}