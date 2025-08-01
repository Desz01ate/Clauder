namespace Clauder.Abstractions;

public interface IToastContext : IDisposable
{
    Task ShowInfoAsync(string message, TimeSpan? duration = null);

    Task ShowSuccessAsync(string message, TimeSpan? duration = null);

    Task ShowWarningAsync(string message, TimeSpan? duration = null);

    Task ShowErrorAsync(string message, TimeSpan? duration = null);

    Task ClearAsync();
}