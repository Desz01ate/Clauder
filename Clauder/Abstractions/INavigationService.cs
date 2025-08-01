namespace Clauder.Abstractions;

public interface INavigationService : IDisposable
{
    Task NavigateToAsync<T>(params object[] args) where T : class, IPage;

    Task NavigateBackAsync();
}