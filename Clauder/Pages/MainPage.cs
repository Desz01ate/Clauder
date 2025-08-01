namespace Clauder.Pages;

using Clauder.Abstractions;
using Clauder.Services;
using Spectre.Console;

public sealed class MainPage : IDisplay
{
    private readonly ClaudeDataService _dataService;
    private readonly NavigationService _navigationService;

    public MainPage()
    {
        this._dataService = new ClaudeDataService();
        this._navigationService = new NavigationService();
    }

    public string Title => this._navigationService.CurrentTitle;

    public async Task DisplayAsync(CancellationToken cancellationToken = default)
    {
        // Start with the projects page
        var projectsPage = new ProjectsPage(this._dataService, this._navigationService);
        await this._navigationService.NavigateToAsync(projectsPage);

        // Run the navigation loop
        await this._navigationService.RunAsync();
    }

    public Task PushBackAsync()
    {
        return this._navigationService.ExitAsync();
    }

    public void Dispose()
    {
        this._navigationService.Dispose();
        this._dataService.Dispose();
    }
}