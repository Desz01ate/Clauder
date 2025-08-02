namespace Clauder.Abstractions;

using Clauder.Models;
using Spectre.Console.Rendering;

public interface IToastManager : IDisposable
{
    IRenderable? CurrentToastDisplay { get; }
    
    event EventHandler<ToastDisplayChangedEventArgs>? ToastDisplayChanged;
    
    Task ProcessToastCommandAsync(ToastCommand command, CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
}

public sealed class ToastDisplayChangedEventArgs : EventArgs
{
    public IRenderable? Display { get; }
    
    public ToastDisplayChangedEventArgs(IRenderable? display)
    {
        this.Display = display;
    }
}