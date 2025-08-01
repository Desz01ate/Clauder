using Clauder.Pages;
using Clauder.Services;
using Clauder.UI;


if (!ClaudeDataService.ClaudeDirectoryExists())
{
    ProjectDisplayService.DisplayErrorMessage($"Claude projects directory not found at: {ClaudeDataService.GetClaudeProjectsPath()}");
    return;
}

var main = new MainPage();

await main.DisplayAsync();

return;

using var dataService = new ClaudeDataService();

// Load initial projects data
await dataService.LoadProjectsAsync();

using var projectDisplayService = new ProjectDisplayService(dataService);
var sessionDisplayService = new SessionDisplayService();
var claudeSessionDisplayService = new ClaudeSessionDisplay();

while (true)
{
    var selectedProject = projectDisplayService.DisplayProjectsPaginated();

    if (selectedProject == null)
    {
        break;
    }

    var (selectedSession, isNewSession) = sessionDisplayService.DisplaySessionsPaginated(selectedProject);

    if (isNewSession)
    {
        await claudeSessionDisplayService.LaunchNewSessionAsync(selectedProject);
    }
    else if (selectedSession != null)
    {
        await claudeSessionDisplayService.LaunchExistingSessionAsync(selectedSession);
    }
}