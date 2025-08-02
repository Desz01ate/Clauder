namespace Clauder.Services;

using Clauder.Abstractions;
using Clauder.Pages;

public sealed class InputProcessor : IInputProcessor
{
    private readonly INavigationContext _navigationContext;
    private readonly IToastContext _toastContext;
    private readonly List<Func<ConsoleKeyInfo, Task<bool>>> _globalHandlers = new();
    private readonly object _handlersLock = new();
    private bool _disposed;

    public InputProcessor(INavigationContext navigationContext, IToastContext toastContext)
    {
        this._navigationContext = navigationContext;
        this._toastContext = toastContext;
    }

    public async Task<bool> ProcessInputAsync(ConsoleKeyInfo keyInfo, IPage? currentPage, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        // First, try global handlers
        var globalHandlers = this.GetGlobalHandlersCopy();
        foreach (var handler in globalHandlers)
        {
            try
            {
                if (await handler(keyInfo))
                    return true;
            }
            catch (Exception ex)
            {
                await this._toastContext.ShowErrorAsync($"Global input handler error: {ex.Message}");
            }
        }
        
        // Then try built-in global navigation
        if (await this.HandleGlobalNavigationAsync(keyInfo))
            return true;
        
        // Finally, let the page handle it
        if (currentPage is IInputHandler inputHandler)
        {
            try
            {
                return await inputHandler.HandleInputAsync(keyInfo, cancellationToken);
            }
            catch (Exception ex)
            {
                await this._toastContext.ShowErrorAsync($"Page input handling error: {ex.Message}");
                return false;
            }
        }
        
        return false;
    }

    public Task RegisterGlobalHandlerAsync(Func<ConsoleKeyInfo, Task<bool>> handler)
    {
        this.ThrowIfDisposed();
        
        lock (this._handlersLock)
        {
            this._globalHandlers.Add(handler);
        }
        
        return Task.CompletedTask;
    }

    public Task UnregisterGlobalHandlerAsync(Func<ConsoleKeyInfo, Task<bool>> handler)
    {
        this.ThrowIfDisposed();
        
        lock (this._handlersLock)
        {
            this._globalHandlers.Remove(handler);
        }
        
        return Task.CompletedTask;
    }

    private List<Func<ConsoleKeyInfo, Task<bool>>> GetGlobalHandlersCopy()
    {
        lock (this._handlersLock)
        {
            return new List<Func<ConsoleKeyInfo, Task<bool>>>(this._globalHandlers);
        }
    }

    private async Task<bool> HandleGlobalNavigationAsync(ConsoleKeyInfo keyInfo)
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

    private void ThrowIfDisposed()
    {
        if (this._disposed)
            throw new ObjectDisposedException(nameof(InputProcessor));
    }

    public void Dispose()
    {
        if (this._disposed)
            return;
            
        this._disposed = true;
        
        lock (this._handlersLock)
        {
            this._globalHandlers.Clear();
        }
    }
}