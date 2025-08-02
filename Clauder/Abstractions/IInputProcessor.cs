namespace Clauder.Abstractions;

public interface IInputProcessor : IDisposable
{
    Task<bool> ProcessInputAsync(ConsoleKeyInfo keyInfo, IPage? currentPage, CancellationToken cancellationToken = default);
    Task RegisterGlobalHandlerAsync(Func<ConsoleKeyInfo, Task<bool>> handler);
    Task UnregisterGlobalHandlerAsync(Func<ConsoleKeyInfo, Task<bool>> handler);
}