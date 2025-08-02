namespace Clauder.Abstractions;

using Spectre.Console.Rendering;

public interface IRenderEngine : IDisposable
{
    Task RunRenderLoopAsync(IRenderable initialLayout, Func<Task<IRenderable>> layoutProvider, Func<ConsoleKeyInfo, Task> inputHandler, Action<Action> registerRefreshCallback, CancellationToken cancellationToken = default);
}