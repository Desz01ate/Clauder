namespace Clauder.Abstractions;

public interface INavigationContext : IDisposable
{
    Task NavigateToAsync<T>(params object[] args) where T : class, IPage;

    Task NavigateBackAsync();
}