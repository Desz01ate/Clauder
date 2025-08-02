namespace Clauder.Commands;

public abstract record ToastCommand;

public sealed record ShowToastCommand(string Message, ToastType Type, TimeSpan Duration) : ToastCommand;

public sealed record ClearToastCommand : ToastCommand;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error,
}