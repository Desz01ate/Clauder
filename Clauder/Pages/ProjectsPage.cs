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

        this._projects = [];
        this._searchFilterSubject = new BehaviorSubject<string>(string.Empty);

        // Combine data service changes with search filter changes
        var filteredProjects = dataService.ProjectSummariesObservable
                                          .CombineLatest(this._searchFilterSubject, (projectData, filter) =>
                                              string.IsNullOrWhiteSpace(filter)
                                                  ? projectData.OrderBy(p => p.ProjectName)
                                                  : projectData.Where(p => p.ProjectName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                                               .OrderBy(p => p.ProjectName));

        // Subscribe to changes to update current filtered projects
        filteredProjects.Subscribe(filtered =>
        {
            this._projects = filtered.ToList();
            this.ResetPagination();
        });

        // Initialize with current projects
        this._projects = dataService.ProjectSummaries;
    }

    public string Title => "[#CC785C]Claude Projects[/]";

    public async Task DisplayAsync(CancellationToken cancellationToken = default)
    {
        await this._dataService.LoadProjectSummariesAsync();

        var layout = this.CreateInitialLayout();
        var shouldExit = false;
        var shouldNavigateToProject = false;
        ClaudeProjectSummary? selectedProject = null;

        await AnsiConsole.Live(layout)
            .StartAsync(async ctx =>
            {
                while (!shouldExit && !shouldNavigateToProject)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Update the display
                    ctx.UpdateTarget(this.CreateCurrentLayout());

                    if (this._projects.Count == 0)
                    {
                        var currentFilter = this._searchFilterSubject.Value;

                        if (string.IsNullOrWhiteSpace(currentFilter))
                        {
                            shouldExit = true;
                            break;
                        }

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
                                shouldExit = true;
                                break;
                        }

                        continue;
                    }

                    var totalPages = (int)Math.Ceiling((double)this._projects.Count / PageSize);

                    // Ensure current page and selection are within bounds
                    this._currentPage = Math.Min(this._currentPage, totalPages - 1);
                    var itemsOnCurrentPage = Math.Min(PageSize, this._projects.Count - this._currentPage * PageSize);
                    this._selectedIndex = Math.Min(this._selectedIndex, itemsOnCurrentPage - 1);

                    var navigationResult = this.ShowProjectNavigation(this._currentPage, totalPages);
                    var handleResult = await this.HandleProjectNavigationResult(navigationResult, this._projects, this._currentPage, this._selectedIndex);

                    if (handleResult != null || navigationResult == NavigationAction.Quit)
                    {
                        if (handleResult != null)
                        {
                            selectedProject = handleResult;
                            shouldNavigateToProject = true;
                        }
                        else
                        {
                            shouldExit = true;
                        }
                    }

                    if (navigationResult is NavigationAction.Search or NavigationAction.Back)
                    {
                        // Update display will happen on next iteration
                        continue;
                    }
                }
            });

        if (selectedProject != null)
        {
            var projectInfo = await this._dataService.LoadProjectSessionsAsync(selectedProject);
            var sessionsPage = new SessionsPage(projectInfo, this._navigationService);
            await this._navigationService.NavigateToAsync(sessionsPage);
        }
        else if (shouldExit)
        {
            await this.PushBackAsync();
        }
    }

    public async Task PushBackAsync()
    {
        await this._navigationService.NavigateBackAsync();
    }

    private Layout CreateInitialLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content"),
                new Layout("Footer").Size(3)
            );

        return layout;
    }

    private Layout CreateCurrentLayout()
    {
        var layout = this.CreateInitialLayout();

        if (this._projects.Count == 0)
        {
            var currentFilter = this._searchFilterSubject.Value;
            
            if (string.IsNullOrWhiteSpace(currentFilter))
            {
                layout["Header"].Update(new Rule("[#CC785C]Claude Projects & Sessions[/]") { Justification = Justify.Left });
                layout["Content"].Update(new Markup("[yellow]No Claude projects found.[/]"));
                layout["Footer"].Update(new Markup("[dim][red]Q[/] Quit[/]"));
            }
            else
            {
                layout["Header"].Update(new Rule("[#CC785C]Claude Projects & Sessions[/]") { Justification = Justify.Left });
                layout["Content"].Update(new Markup($"[yellow]No projects found matching '{currentFilter}'.[/]"));
                layout["Footer"].Update(this.CreateSearchNavigationMarkup());
            }
        }
        else
        {
            var totalPages = (int)Math.Ceiling((double)this._projects.Count / PageSize);
            var titleText = string.IsNullOrEmpty(this._searchFilterSubject.Value)
                ? $"[#CC785C]Claude Projects & Sessions[/] [dim]({this._currentPage + 1}/{totalPages})[/]"
                : $"[#CC785C]Claude Projects & Sessions[/] [dim]({this._currentPage + 1}/{totalPages}) - Filtered by: '{this._searchFilterSubject.Value}'[/]";

            layout["Header"].Update(new Rule(titleText) { Justification = Justify.Left });
            layout["Content"].Update(this.CreateProjectTable());
            layout["Footer"].Update(this.CreateNavigationMarkup(this._currentPage, totalPages));
        }

        return layout;
    }

    private Table CreateProjectTable()
    {
        var table = new Table().Expand();
        table.AddColumn("[bold]#[/]");
        table.AddColumn("[bold]Project[/]");
        table.AddColumn("[bold]Path[/]");
        table.AddColumn("[bold]Sessions[/]");
        table.AddColumn("[bold]Git Branch[/]");

        var pageProjects = this._projects.Skip(this._currentPage * PageSize).Take(PageSize);

        var i = 0;
        foreach (var project in pageProjects)
        {
            var sessionCount = project.SessionCount;

            var sessionCountText = sessionCount == 1
                ? "[dim]1 session[/]"
                : $"[dim]{sessionCount} sessions[/]";

            var relativePath = project.ProjectPath.Replace(Environment.GetEnvironmentVariable("HOME")!, "~");

            var isSelected = i == this._selectedIndex;
            var selectionMarker = isSelected ? "[yellow]>[/]" : " ";
            var projectName = isSelected ? $"[yellow]{project.ProjectName}[/]" : $"[dim]{project.ProjectName}[/]";

            table.AddRow(
                selectionMarker,
                projectName,
                $"[dim]{relativePath}[/]",
                sessionCountText,
                $"[dim]{project.LastGitBranch ?? "N/A"}[/]"
            );
            i++;
        }

        return table;
    }

    private Markup CreateNavigationMarkup(int currentPage, int totalPages)
    {
        var navigationItems = new[]
        {
            "[green]↑↓[/] Select",
            "[green]Enter[/] View Sessions",
            "[yellow]S[/] Search",
        };
        var navigationText = new List<string>(navigationItems);

        if (!string.IsNullOrEmpty(this._searchFilterSubject.Value))
            navigationText.Add("[yellow]C[/] Clear Search");

        if (currentPage > 0)
            navigationText.Add("[blue]←[/] Previous ");
        if (currentPage < totalPages - 1)
            navigationText.Add(" Next [blue]→[/]");
        navigationText.Add("[red]Q[/] Quit");

        var footerContent = $"[dim]{string.Join(" • ", navigationText)}[/]\n";
        footerContent += $"[dim]Total: {this._projects.Count} projects, {this._projects.Sum(p => p.SessionCount)} sessions[/]";

        return new Markup(footerContent);
    }

    private Markup CreateSearchNavigationMarkup()
    {
        var navigationText = new[]
        {
            "[yellow]S[/] Search Again",
            "[yellow]C[/] Clear Search",
            "[red]Q[/] Quit",
        };

        return new Markup($"[dim]{string.Join(" • ", navigationText)}[/]");
    }

    private NavigationAction ShowProjectNavigation(int currentPage, int totalPages)
    {
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
        // Temporarily exit Live context for input
        Console.Write("Enter search term (project name): ");
        var searchTerm = Console.ReadLine() ?? string.Empty;
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


    public static void DisplayErrorMessage(string message)
    {
        AnsiConsole.MarkupLine($"[red]{message}[/]");
    }

    public void Dispose()
    {
        this._searchFilterSubject.Dispose();
    }
}