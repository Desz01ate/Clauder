namespace Clauder.Services;

using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;

public sealed class ToastContext : IToastContext
{
    private readonly ChannelWriter<ToastCommand> _toastWriter;

    public ToastContext(ChannelWriter<ToastCommand> toastWriter)
    {
        this._toastWriter = toastWriter;
    }

    public async Task ShowInfoAsync(string message, TimeSpan? duration = null)
    {
        var command = new ShowToastCommand(message, ToastType.Info, duration ?? TimeSpan.FromSeconds(5));
        await this._toastWriter.WriteAsync(command);
    }

    public async Task ShowSuccessAsync(string message, TimeSpan? duration = null)
    {
        var command = new ShowToastCommand(message, ToastType.Success, duration ?? TimeSpan.FromSeconds(5));
        await this._toastWriter.WriteAsync(command);
    }

    public async Task ShowWarningAsync(string message, TimeSpan? duration = null)
    {
        var command = new ShowToastCommand(message, ToastType.Warning, duration ?? TimeSpan.FromSeconds(5));
        await this._toastWriter.WriteAsync(command);
    }

    public async Task ShowErrorAsync(string message, TimeSpan? duration = null)
    {
        var command = new ShowToastCommand(message, ToastType.Error, duration ?? TimeSpan.FromSeconds(5));
        await this._toastWriter.WriteAsync(command);
    }

    public async Task ClearAsync()
    {
        var command = new ClearToastCommand();
        await this._toastWriter.WriteAsync(command);
    }

    public void Dispose()
    {
        this._toastWriter.Complete();
    }
}