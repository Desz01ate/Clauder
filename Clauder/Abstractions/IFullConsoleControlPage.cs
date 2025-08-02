namespace Clauder.Abstractions;

/// <summary>
/// Marker interface for pages that need full control of the console (like external process pages).
/// Pages implementing this interface will bypass the normal rendering loop.
/// </summary>
public interface IFullConsoleControlPage : IPage
{
    Task RunAsync(CancellationToken cancellationToken = default);
}