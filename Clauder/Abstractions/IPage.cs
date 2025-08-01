namespace Clauder.Abstractions;

using Spectre.Console.Rendering;

public interface IPage : IDisposable
{
    string Title { get; }

    ValueTask<IRenderable> RenderHeaderAsync();

    ValueTask<IRenderable> RenderBodyAsync();

    ValueTask<IRenderable> RenderFooterAsync();

    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}