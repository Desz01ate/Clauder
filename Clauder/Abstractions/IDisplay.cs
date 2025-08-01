namespace Clauder.Abstractions;

public interface IDisplay : IDisposable
{
    string Title { get; }

    Task DisplayAsync(CancellationToken cancellationToken = default);
}