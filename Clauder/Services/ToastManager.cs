namespace Clauder.Services;

using Clauder.Abstractions;
using Clauder.Models;
using Commands;
using Spectre.Console;
using Spectre.Console.Rendering;

internal record struct ToastDisplayInfo(string Message, DateTime Time, ToastType Type, TimeSpan Duration);

public sealed class ToastManager : IToastManager
{
    private readonly Queue<ToastDisplayInfo> _toastQueue = new();
    private readonly object _toastLock = new();
    private ToastDisplayInfo? _currentToast;
    private CancellationTokenSource? _currentToastCancellation;
    private bool _disposed;
    private Task? _processingTask;

    public IRenderable? CurrentToastDisplay { get; private set; }

    public event EventHandler<ToastDisplayChangedEventArgs>? ToastDisplayChanged;

    public Task ProcessToastCommandAsync(ToastCommand command, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        switch (command)
        {
            case ShowToastCommand showCommand:
                var toastInfo = new ToastDisplayInfo(
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
                    this._toastQueue.Clear();
                    this._currentToast = null;
                }
                this._currentToastCancellation?.Cancel();
                this.UpdateToastDisplay(null);
                break;
        }
        
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        this._processingTask = this.ProcessToastsAsync(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task ProcessToastsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ToastDisplayInfo? nextToast = null;

            lock (this._toastLock)
            {
                if (this._currentToast == null && this._toastQueue.Count > 0)
                {
                    nextToast = this._toastQueue.Dequeue();
                    this._currentToast = nextToast;
                }
            }

            if (nextToast.HasValue)
            {
                this._currentToastCancellation?.Cancel();
                this._currentToastCancellation = new CancellationTokenSource();

                var toastToken = this._currentToastCancellation.Token;

                this.UpdateToastDisplay(this.CreateToastDisplay(nextToast.Value));

                try
                {
                    await Task.Delay(nextToast.Value.Duration, toastToken);

                    lock (this._toastLock)
                    {
                        if (this._currentToast?.Time == nextToast.Value.Time)
                        {
                            this._currentToast = null;
                            this.UpdateToastDisplay(null);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Toast was cancelled
                }
            }
            else if (this._currentToast == null)
            {
                this.UpdateToastDisplay(null);
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private IRenderable CreateToastDisplay(ToastDisplayInfo toast)
    {
        var toastText = toast.Message.Length > 55
            ? toast.Message[..52] + "..."
            : toast.Message;

        var (icon, color) = toast.Type switch
        {
            ToastType.Success => ("✓", "green"),
            ToastType.Warning => ("⚠", "yellow"),
            ToastType.Error => ("✗", "red"),
            _ => ("ℹ", "blue"),
        };

        var panel = new Panel($"[{color}]{icon} {toastText}[/]")
                    .RoundedBorder()
                    .BorderColor(Color.FromInt32(toast.Type switch
                    {
                        ToastType.Success => 28,
                        ToastType.Warning => 178,
                        ToastType.Error => 196,
                        _ => 33,
                    }))
                    .Padding(0, 0);

        return panel;
    }

    private void UpdateToastDisplay(IRenderable? display)
    {
        this.CurrentToastDisplay = display;
        this.ToastDisplayChanged?.Invoke(this, new ToastDisplayChangedEventArgs(display));
    }

    private void ThrowIfDisposed()
    {
        if (this._disposed)
            throw new ObjectDisposedException(nameof(ToastManager));
    }

    public void Dispose()
    {
        if (this._disposed)
            return;
            
        this._disposed = true;
        
        this._currentToastCancellation?.Cancel();
        this._currentToastCancellation?.Dispose();
        
        if (this._processingTask != null)
        {
            try
            {
                this._processingTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }
}