namespace Clauder.Abstractions;

public interface IInputHandler
{
    Task<bool> HandleInputAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default);
}