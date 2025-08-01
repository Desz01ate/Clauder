namespace Clauder.Abstractions;

public interface INavigationService
{
    Task NavigateToAsync<T>(T page) where T : IDisplay;

    Task NavigateBackAsync();

    Task ExitAsync();
}