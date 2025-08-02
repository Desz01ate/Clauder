namespace Clauder.Pages;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Clauder.Models;
using Clauder.Services;
using Conspectre.Abstractions;
using Enums;
using Spectre.Console;
using Spectre.Console.Rendering;

public sealed class ProjectsPage : IPage, IInputHandler
{
    private readonly ClaudeDataService _dataService;
    private readonly INavigationContext _navigationContext;
    private readonly IInputProcessor _inputProcessor;

    private IReadOnlyList<ClaudeProjectSummary> _projects;
    private int _currentPage;
    private int _selectedIndex;
    private readonly BehaviorSubject<string> _searchFilterSubject;
    private bool _isSearchMode;
    private string _searchInput;

    private static int PageSize
    {
        get
        {
            var consoleHeight = Console.WindowHeight;

            return (int)(consoleHeight * 0.7);
        }
    }

    public ProjectsPage(ClaudeDataService dataService, INavigationContext navigationContext, IInputProcessor inputProcessor)
    {
        this._dataService = dataService;
        this._navigationContext = navigationContext;
        this._inputProcessor = inputProcessor;

        this._projects = [];
        this._searchFilterSubject = new BehaviorSubject<string>(string.Empty);
        this._isSearchMode = false;
        this._searchInput = string.Empty;

        // Combine data service changes with search filter changes
        var filteredProjects =
            dataService.ProjectSummariesObservable
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

        // Register a global handler for search mode that takes priority
        this._inputProcessor.RegisterGlobalHandlerAsync(this.HandleSearchModeGlobally);
    }

    public string Title => "[#CC785C]Claude Projects[/]";

    public ValueTask<IRenderable> RenderHeaderAsync()
    {
        var totalPages = (int)Math.Ceiling((double)this._projects.Count / PageSize);
        var titleText = $"[#CC785C]Claude Projects & Sessions[/] [dim]({this._currentPage + 1}/{totalPages})[/]";

        var header = new Rule(titleText) { Justification = Justify.Left };

        return ValueTask.FromResult<IRenderable>(header);
    }

    public ValueTask<IRenderable> RenderBodyAsync()
    {
        if (this._projects.Count == 0)
        {
            var currentFilter = this._searchFilterSubject.Value;
            var message = string.IsNullOrWhiteSpace(currentFilter)
                ? "[yellow]No Claude projects found.[/]"
                : $"[yellow]No projects found matching '{currentFilter}'.[/]";

            return ValueTask.FromResult<IRenderable>(new Markup(message));
        }

        var body = this.CreateProjectTable();

        return ValueTask.FromResult<IRenderable>(body);
    }

    public ValueTask<IRenderable> RenderFooterAsync()
    {
        if (this._projects.Count == 0)
        {
            var currentFilter = this._searchFilterSubject.Value;
            var emptyFooter = string.IsNullOrWhiteSpace(currentFilter)
                ? new Markup("[dim][red]B[/] Quit[/]")
                : CreateSearchNavigationMarkup();

            return ValueTask.FromResult<IRenderable>(emptyFooter);
        }

        var totalPages = (int)Math.Ceiling((double)this._projects.Count / PageSize);
        var footer = this.CreateNavigationMarkup(this._currentPage, totalPages);

        return ValueTask.FromResult<IRenderable>(footer);
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        await this._dataService.LoadProjectSummariesAsync();
    }

    public async Task<bool> HandleInputAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        // Handle search mode input first
        if (this._isSearchMode)
        {
            return this.HandleSearchInput(keyInfo);
        }

        var totalPages = (int)Math.Ceiling((double)this._projects.Count / PageSize);

        // Ensure current page and selection are within bounds
        this._currentPage = Math.Min(this._currentPage, totalPages - 1);
        var itemsOnCurrentPage = Math.Min(PageSize, this._projects.Count - this._currentPage * PageSize);
        this._selectedIndex = Math.Min(this._selectedIndex, itemsOnCurrentPage - 1);

        var action = keyInfo.Key switch
        {
            ConsoleKey.UpArrow => NavigationAction.SelectUp,
            ConsoleKey.DownArrow => NavigationAction.SelectDown,
            ConsoleKey.Enter => NavigationAction.SelectItem,
            ConsoleKey.RightArrow when this._currentPage < totalPages - 1 => NavigationAction.NextPage,
            ConsoleKey.LeftArrow when this._currentPage > 0 => NavigationAction.PreviousPage,
            ConsoleKey.S => NavigationAction.Search,
            ConsoleKey.O => NavigationAction.Settings,
            ConsoleKey.C when !string.IsNullOrWhiteSpace(this._searchFilterSubject.Value) => NavigationAction.Back,
            _ => NavigationAction.None,
        };

        if (action != NavigationAction.None)
        {
            await this.HandleProjectNavigationResult(action);
            return true;
        }

        return false;
    }

    private bool HandleSearchInput(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Enter:
                this.ApplySearch();
                this.ResetPagination();
                return true;

            case ConsoleKey.Escape:
                this.CancelSearch();
                return true;

            case ConsoleKey.Backspace:
                if (this._searchInput.Length > 0)
                {
                    this._searchInput = this._searchInput[..^1];
                }

                return true;

            default:
                // Add printable characters to search input
                if (!char.IsControl(keyInfo.KeyChar))
                {
                    this._searchInput += keyInfo.KeyChar;
                }

                return true;
        }
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

        var currentFilter = this._searchFilterSubject.Value;

        if (!string.IsNullOrWhiteSpace(currentFilter))
        {
            navigationText.Add("[yellow]C[/] Clear Search");
        }

        navigationText.Add("[red]B[/] Quit");
        navigationText.Add("[blue]O[/] Settings");

        if (currentPage > 0)
        {
            navigationText.Add("[blue]←[/] Previous ");
        }

        if (currentPage < totalPages - 1)
        {
            navigationText.Add(" Next [blue]→[/]");
        }

        var footerContent = $"[dim]{string.Join(" • ", navigationText)}[/]\n";

        // Show search input when in search mode, otherwise show applied filter
        if (this._isSearchMode)
        {
            footerContent += $"[dim]Search: [/][yellow]{this._searchInput}[/][dim]|[/]\n";
        }
        else if (!string.IsNullOrWhiteSpace(currentFilter))
        {
            footerContent += $"[dim]Filtered by: '[/][yellow]{currentFilter}[/][dim]'[/]\n";
        }

        footerContent += $"[dim]Total: {this._projects.Count} projects, {this._projects.Sum(p => p.SessionCount)} sessions[/]";

        return new Markup(footerContent);
    }

    private Markup CreateSearchNavigationMarkup()
    {
        var navigationText = new[]
        {
            "[yellow]S[/] Search Again",
            "[yellow]C[/] Clear Search",
            "[red]B[/] Quit",
        };

        var footerContent = $"[dim]{string.Join(" • ", navigationText)}[/]";

        // Show search input when in search mode, otherwise show applied filter
        if (this._isSearchMode)
        {
            footerContent += $"\n[dim]Search: [/][yellow]{this._searchInput}[/][dim]|[/]";
        }
        else
        {
            var currentFilter = this._searchFilterSubject.Value;

            if (!string.IsNullOrWhiteSpace(currentFilter))
            {
                footerContent += $"\n[dim]Filtered by: '[/][yellow]{currentFilter}[/][dim]'[/]";
            }
        }

        return new Markup(footerContent);
    }

    private async Task HandleProjectNavigationResult(NavigationAction action)
    {
        var itemsOnPage = Math.Min(PageSize, this._projects.Count - this._currentPage * PageSize);

        switch (action)
        {
            case NavigationAction.SelectUp:
            {
                this._selectedIndex = this._selectedIndex > 0 ? this._selectedIndex - 1 : itemsOnPage - 1;
                break;
            }
            case NavigationAction.SelectDown:
            {
                this._selectedIndex = this._selectedIndex < itemsOnPage - 1 ? this._selectedIndex + 1 : 0;
                break;
            }
            case NavigationAction.SelectItem:
            {
                var actualIndex = this._currentPage * PageSize + this._selectedIndex;
                var project = this._projects[actualIndex];

                var projectInfo = await this._dataService.LoadProjectSessionsAsync(project);

                await this._navigationContext.NavigateToAsync<SessionsPage>(projectInfo);

                break;
            }
            case NavigationAction.NextPage:
            {
                this._currentPage++;
                this._selectedIndex = Math.Clamp(this._selectedIndex, 0, itemsOnPage - 1);
                break;
            }
            case NavigationAction.PreviousPage:
            {
                this._currentPage--;
                this._selectedIndex = Math.Clamp(this._selectedIndex, 0, itemsOnPage - 1);
                break;
            }
            case NavigationAction.Search:
            {
                this.StartSearchMode();
                break;
            }
            case NavigationAction.Settings:
            {
                await this._navigationContext.NavigateToAsync<SettingsPage>();

                break;
            }
            case NavigationAction.Back:
            {
                this.ClearSearch();
                this.ResetPagination();
                break;
            }
            case NavigationAction.Quit:
            {
                await this._navigationContext.NavigateBackAsync();
                break;
            }
        }
    }

    private void StartSearchMode()
    {
        this._isSearchMode = true;
        this._searchInput = this._searchFilterSubject.Value; // Start with current filter
    }

    private void ApplySearch()
    {
        this._isSearchMode = false;
        var trimmedSearchTerm = this._searchInput.Trim();

        // Only update if the search term has actually changed
        if (trimmedSearchTerm != this._searchFilterSubject.Value)
        {
            this._searchFilterSubject.OnNext(trimmedSearchTerm);
        }
    }

    private void CancelSearch()
    {
        this._isSearchMode = false;
        this._searchInput = string.Empty;
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

    private Task<bool> HandleSearchModeGlobally(ConsoleKeyInfo keyInfo)
    {
        if (this._isSearchMode)
        {
            this.HandleSearchInput(keyInfo);

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public void Dispose()
    {
        this._inputProcessor.UnregisterGlobalHandlerAsync(this.HandleSearchModeGlobally);

        this._searchFilterSubject.Dispose();
    }
}