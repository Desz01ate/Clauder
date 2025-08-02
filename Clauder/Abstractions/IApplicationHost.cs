namespace Clauder.Abstractions;

public interface IApplicationHost : IDisposable
{
    Task RunAsync(CancellationToken cancellationToken = default);
}