namespace Clauder.Pages;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Clauder.Abstractions;
using Clauder.Enums;
using Clauder.Models;
using Clauder.Services;
using Spectre.Console;

public sealed class ProjectsPage : IDisplay
{
    private readonly ClaudeDataService _dataService;
    private readonly INavigationService _navigationService;

    private IReadOnlyList<ClaudeProjectSummary> _projects;
    private const int PageSize = 10;
    private int _currentPage;
    private int _selectedIndex;
    private readonly BehaviorSubject<string> _searchFilterSubject;

    public ProjectsPage(ClaudeDataService dataService, INavigationService navigationService)
    {
        this._dataService = dataService;
        this._navigationService = navigationService;

        this._projects = new List<ClaudeProjectSummary>();
        this._searchFilterSubject = new BehaviorSubject<string>(string.Empty);

        // Combine data service changes with search filter changes
        var filteredProjects = dataService.ProjectSummariesObservable
                                          .CombineLatest(this._searchFilterSubject, (projectData, filter) =>
                                              string.IsNullOrWhiteSpace(filter)
                                                  ? projectData.OrderBy(p => p.ProjectName).ToList()
                                                  : projectData.Where(p => p.ProjectName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                                               .OrderBy(p => p.ProjectName).ToList());

        // Subscribe to changes to update current filtered projects
        filteredProjects.Subscribe(filtered =>
        {
            this._projects = filtered;
            this.ResetPagination();
        });

        // Initialize with current projects
        this._projects = dataService.ProjectSummaries;
    }

    public string Title => "[#CC785C]Claude Projects[/]";

    public async Task DisplayAsync(CancellationToken cancellationToken = default)
    {
        await this._dataService.LoadProjectSummariesAsync();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnsiConsole.WriteLine();

            var rule = new Rule("[#CC785C]Claude Projects & Sessions[/]")
            {
                Justification = Justify.Left,
            };

            AnsiConsole.Write(rule);

            if (this._projects.Count == 0)
            {
                var currentFilter = this._searchFilterSubject.Value;

                if (string.IsNullOrWhiteSpace(currentFilter))
                {
                    AnsiConsole.MarkupLine("[yellow]No Claude projects found.[/]");
                    await this.PushBackAsync();

                    return;
                }

                AnsiConsole.MarkupLine($"[yellow]No projects found matching '{currentFilter}'.[/]");
                AnsiConsole.WriteLine();

                ShowSearchNavigation();

                var key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.S:
                        this.PromptForSearch();
                        break;
                    case ConsoleKey.C:
                        this.ClearSearch();
                        break;
                    case ConsoleKey.Q:
                        await this.PushBackAsync();
                        return;
                }

                continue;
            }

            var totalPages = (int)Math.Ceiling((double)this._projects.Count / PageSize);

            // Ensure current page and selection are within bounds
            this._currentPage = Math.Min(this._currentPage, totalPages - 1);
            var itemsOnCurrentPage = Math.Min(PageSize, this._projects.Count - this._currentPage * PageSize);
            this._selectedIndex = Math.Min(this._selectedIndex, itemsOnCurrentPage - 1);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AnsiConsole.Clear();
                AnsiConsole.WriteLine();

                DisplayPageWithSelection(this._projects, this._currentPage, totalPages, this._selectedIndex, this._searchFilterSubject.Value);

                if (totalPages <= 1 && this._projects.Count <= PageSize)
                {
                    var result = this.ShowProjectNavigation(this._currentPage, totalPages);
                    var singlePageResult = await this.HandleProjectNavigationResult(result, this._projects, this._currentPage, this._selectedIndex);

                    if (singlePageResult != null || result == NavigationAction.Quit)
                    {
                        if (singlePageResult != null)
                        {
                            var projectInfo = await this._dataService.LoadProjectSessionsAsync(singlePageResult.ProjectPath);
                            var sessionsPage = new SessionsPage(projectInfo, this._navigationService);

                            await this._navigationService.NavigateToAsync(sessionsPage);
                        }
                        else
                        {
                            await this.PushBackAsync();
                            return;
                        }
                    }

                    if (result is NavigationAction.Search or NavigationAction.Back)
                    {
                        break;
                    }
                }
                else
                {
                    var navigationResult = this.ShowProjectNavigation(this._currentPage, totalPages);
                    var handleResult = await this.HandleProjectNavigationResult(navigationResult, this._projects, this._currentPage, this._selectedIndex);

                    if (handleResult != null || navigationResult == NavigationAction.Quit)
                    {
                        if (handleResult != null)
                        {
                            var projectInfo = await this._dataService.LoadProjectSessionsAsync(handleResult.ProjectPath);
                            var sessionsPage = new SessionsPage(projectInfo, this._navigationService);

                            await this._navigationService.NavigateToAsync(sessionsPage);
                        }
                        else
                        {
                            await this.PushBackAsync();
                            return;
                        }
                    }

                    if (navigationResult is NavigationAction.Search or NavigationAction.Back)
                    {
                        break;
                    }
                }
            }
        }
    }

    public async Task PushBackAsync()
    {
        await this._navigationService.NavigateBackAsync();
    }

    private static void DisplayPageWithSelection(
        IReadOnlyList<ClaudeProjectSummary> projects,
        int currentPage,
        int totalPages,
        int selectedIndex,
        string searchFilter = "")
    {
        var titleText = string.IsNullOrEmpty(searchFilter)
            ? $"[#CC785C]Claude Projects & Sessions[/] [dim]({currentPage + 1}/{totalPages})[/]"
            : $"[#CC785C]Claude Projects & Sessions[/] [dim]({currentPage + 1}/{totalPages}) - Filtered by: '{searchFilter}'[/]";

        var pageRule = new Rule(titleText)
        {
            Justification = Justify.Left,
        };

        AnsiConsole.Write(pageRule);

        var table = new Table();
        table.AddColumn("[bold]#[/]");
        table.AddColumn("[bold]Project[/]");
        table.AddColumn("[bold]Path[/]");
        table.AddColumn("[bold]Sessions[/]");
        table.AddColumn("[bold]Git Branch[/]");

        var pageProjects = projects.Skip(currentPage * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < pageProjects.Count; i++)
        {
            var project = pageProjects[i];
            var sessionCount = project.SessionCount;

            var sessionCountText = sessionCount == 1
                ? "[dim]1 session[/]"
                : $"[dim]{sessionCount} sessions[/]";

            var relativePath = project.ProjectPath.Replace(Environment.GetEnvironmentVariable("HOME")!, "~");

            var isSelected = i == selectedIndex;
            var selectionMarker = isSelected ? "[yellow]>[/]" : " ";
            var projectName = isSelected ? $"[yellow]{project.ProjectName}[/]" : $"[bold green]{project.ProjectName}[/]";

            table.AddRow(
                selectionMarker,
                projectName,
                $"[dim]{relativePath}[/]",
                sessionCountText,
                $"[dim]{project.LastGitBranch ?? "N/A"}[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total: {projects.Count} projects, {projects.Sum(p => p.SessionCount)} sessions[/]");
    }

    private NavigationAction ShowProjectNavigation(int currentPage, int totalPages)
    {
        AnsiConsole.WriteLine();
        var navigationText = new List<string>
        {
            "[green]↑↓[/] Select",
            "[green]Enter[/] View Sessions",
            "[yellow]S[/] Search",
        };

        if (!string.IsNullOrEmpty(this._searchFilterSubject.Value))
            navigationText.Add("[yellow]C[/] Clear Search");

        if (currentPage > 0)
            navigationText.Add("[blue]←[/] Previous ");
        if (currentPage < totalPages - 1)
            navigationText.Add(" Next [blue]→[/]");
        navigationText.Add("[red]Q[/] Quit");

        AnsiConsole.MarkupLine($"[dim]{string.Join(" • ", navigationText)}[/]");

        var key = Console.ReadKey(true).Key;

        return key switch
        {
            ConsoleKey.UpArrow => NavigationAction.SelectUp,
            ConsoleKey.DownArrow => NavigationAction.SelectDown,
            ConsoleKey.Enter => NavigationAction.SelectItem,
            ConsoleKey.RightArrow when currentPage < totalPages - 1 => NavigationAction.NextPage,
            ConsoleKey.LeftArrow when currentPage > 0 => NavigationAction.PreviousPage,
            ConsoleKey.S => NavigationAction.Search,
            ConsoleKey.C when !string.IsNullOrEmpty(this._searchFilterSubject.Value) => NavigationAction.Back,
            ConsoleKey.Q => NavigationAction.Quit,
            _ => NavigationAction.None,
        };
    }

    private Task<ClaudeProjectSummary?> HandleProjectNavigationResult(
        NavigationAction action,
        IReadOnlyList<ClaudeProjectSummary> sortedProjects,
        int currentPage,
        int selectedIndex)
    {
        var itemsOnPage = Math.Min(PageSize, sortedProjects.Count - currentPage * PageSize);

        switch (action)
        {
            case NavigationAction.SelectUp:
                this._selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : itemsOnPage - 1;
                break;
            case NavigationAction.SelectDown:
                this._selectedIndex = selectedIndex < itemsOnPage - 1 ? selectedIndex + 1 : 0;
                break;
            case NavigationAction.SelectItem:
                var actualIndex = currentPage * PageSize + selectedIndex;
                return Task.FromResult<ClaudeProjectSummary?>(sortedProjects[actualIndex]);
            case NavigationAction.NextPage:
                this._currentPage++;
                this._selectedIndex = Math.Clamp(this._selectedIndex, 0, itemsOnPage - 1);
                break;
            case NavigationAction.PreviousPage:
                this._currentPage--;
                this._selectedIndex = Math.Clamp(this._selectedIndex, 0, itemsOnPage - 1);
                break;
            case NavigationAction.Search:
                this.PromptForSearch();
                this.ResetPagination();
                break;
            case NavigationAction.Back:
                this.ClearSearch();
                this.ResetPagination();
                break;
            case NavigationAction.Quit:
                break;
        }

        return Task.FromResult<ClaudeProjectSummary?>(null);
    }

    private void PromptForSearch()
    {
        AnsiConsole.WriteLine();
        var searchTerm = AnsiConsole.Ask<string>("[yellow]Enter search term (project name):[/]");
        this._searchFilterSubject.OnNext(searchTerm.Trim());
    }

    private void ClearSearch()
    {
        this._searchFilterSubject.OnNext(string.Empty);
    }

    private void ResetPagination()
    {
        this._currentPage = 0;
        this._selectedIndex = 0;
    }

    private static void ShowSearchNavigation()
    {
        var navigationText = new List<string>
        {
            "[yellow]S[/] Search Again",
            "[yellow]C[/] Clear Search",
            "[red]Q[/] Quit",
        };

        AnsiConsole.MarkupLine($"[dim]{string.Join(" • ", navigationText)}[/]");
    }

    public static void DisplayErrorMessage(string message)
    {
        AnsiConsole.MarkupLine($"[red]{message}[/]");
    }

    public void Dispose()
    {
        this._searchFilterSubject.Dispose();
    }
}