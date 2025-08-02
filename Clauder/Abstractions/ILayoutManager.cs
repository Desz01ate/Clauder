namespace Clauder.Abstractions;

using Spectre.Console.Rendering;

public interface ILayoutManager : IDisposable
{
    Task<IRenderable> CreateLayoutAsync(IPage page, IRenderable? toastDisplay = null, CancellationToken cancellationToken = default);
}