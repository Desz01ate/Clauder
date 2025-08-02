namespace Clauder.Pages;

using Clauder.Abstractions;
using Clauder.Enums;
using Clauder.Models;
using Conspectre.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;

public sealed class SessionsPage : IPage, IInputHandler
{
    private readonly ClaudeProjectWithSessions _project;
    private readonly INavigationContext _navigationContext;
    private readonly IToastContext _toastContext;

    private int _currentPage;
    private int _selectedIndex;

    private static int PageSize
    {
        get
        {
            var consoleHeight = Console.WindowHeight;

            return (int)(consoleHeight * 0.7);
        }
    }

    public SessionsPage(
        ClaudeProjectWithSessions project,
        INavigationContext navigationContext,
        IToastContext toastContext)
    {
        this._project = project;
        this._navigationContext = navigationContext;
        this._toastContext = toastContext;
    }

    public string Title => $"[#CC785C]Sessions - {this._project.ProjectName}[/]";

    public ValueTask<IRenderable> RenderHeaderAsync()
    {
        var sessionCount = this._project.Sessions.Count;
        var totalPages = (int)Math.Ceiling((double)sessionCount / PageSize);

        var pageRule = new Rule($"[#CC785C]Sessions for {this._project.ProjectName}[/] [dim]({this._currentPage + 1}/{totalPages})[/]")
        {
            Justification = Justify.Left,
        };

        return ValueTask.FromResult<IRenderable>(pageRule);
    }

    public ValueTask<IRenderable> RenderBodyAsync()
    {
        var sortedSessions = this._project.Sessions.OrderByDescending(s => s.Timestamp).ToList();
        var body = this.CreateSessionTable(sortedSessions);

        return ValueTask.FromResult<IRenderable>(body);
    }

    public ValueTask<IRenderable> RenderFooterAsync()
    {
        var sessionCount = this._project.Sessions.Count;
        var totalPages = (int)Math.Ceiling((double)sessionCount / PageSize);
        var footer = this.CreateSessionNavigationMarkup(this._currentPage, totalPages);

        return ValueTask.FromResult<IRenderable>(footer);
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Sessions are already loaded when the page is created with ClaudeProjectInfo
        // But we can ensure proper bounds are set
        var sessionCount = this._project.Sessions.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)sessionCount / PageSize));

        this._currentPage = Math.Max(0, Math.Min(this._currentPage, totalPages - 1));
        var itemsOnCurrentPage = Math.Min(PageSize, sessionCount - this._currentPage * PageSize);
        this._selectedIndex = Math.Max(0, Math.Min(this._selectedIndex, Math.Max(0, itemsOnCurrentPage - 1)));

        return ValueTask.CompletedTask;
    }

    public async Task<bool> HandleInputAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        var sortedSessions = this._project.Sessions.OrderByDescending(s => s.Timestamp).ToList();
        var sessionCount = sortedSessions.Count;
        var totalPages = (int)Math.Ceiling((double)sessionCount / PageSize);

        // Ensure current page and selection are within bounds
        this._currentPage = Math.Min(this._currentPage, totalPages - 1);
        var itemsOnCurrentPage = Math.Min(PageSize, sessionCount - this._currentPage * PageSize);
        this._selectedIndex = Math.Min(this._selectedIndex, itemsOnCurrentPage - 1);

        var navigationResult = ShowSessionNavigation(keyInfo, this._currentPage, totalPages);

        if (navigationResult != NavigationAction.None)
        {
            await this.HandleSessionNavigationResult(navigationResult, sortedSessions);
            return true;
        }

        return false;
    }


    private Table CreateSessionTable(List<ClaudeSessionMetadata> sessions)
    {
        var table = new Table().Expand();
        table.AddColumn("[bold]#[/]");
        table.AddColumn("[bold]Session ID[/]");
        table.AddColumn("[bold]Timestamp[/]");
        table.AddColumn("[bold]Git Branch[/]");
        table.AddColumn("[bold]Type[/]");
        table.AddColumn("[bold]Message Preview[/]");

        var pageSessions = sessions.Skip(this._currentPage * PageSize).Take(PageSize);

        var i = 0;

        foreach (var session in pageSessions)
        {
            var sessionIdShort = session.SessionId?.Length > 8 ? session.SessionId[..8] + "..." : session.SessionId ?? "N/A";
            var messagePreview = session.Message?.Content?.Length > 50
                ? session.Message.Content[..50] + "..."
                : session.Message?.Content ?? "N/A";

            var isSelected = i == this._selectedIndex;
            var selectionMarker = isSelected ? "[yellow]>[/]" : " ";
            var sessionId = isSelected ? $"[yellow]{sessionIdShort}[/]" : $"[dim]{sessionIdShort}[/]";

            // Highlight different session types
            var isCorrupted = session.Type == "corrupted";
            var isAISummary = session.Type == "ai-summary";

            string typeDisplay, messageDisplay;

            if (isAISummary)
            {
                typeDisplay = "[blue]AI Summary[/]";
                messageDisplay = "[blue]AI Generated Summary[/]";
            }
            else if (isCorrupted)
            {
                typeDisplay = "[red]Corrupted[/]";
                messageDisplay = "[red]Invalid or Corrupted Session File[/]";
            }
            else
            {
                typeDisplay = $"[dim]{session.Type ?? "N/A"}[/]";
                messageDisplay = $"[dim]{messagePreview}[/]";
            }

            table.AddRow(
                selectionMarker,
                sessionId,
                $"[dim]{session.Timestamp:yyyy-MM-dd HH:mm:ss}[/]",
                $"[dim]{session.GitBranch ?? "N/A"}[/]",
                typeDisplay,
                messageDisplay
            );
            i++;
        }

        return table;
    }

    private Markup CreateSessionNavigationMarkup(int currentPage, int totalPages)
    {
        var navigationItems = new[]
        {
            "[green]↑↓[/] Select",
            "[green]Enter[/] Resume Session",
            "[cyan]N[/] New Session",
            "[red]B[/] Back",
        };
        var navigationText = new List<string>(navigationItems);

        if (currentPage > 0)
            navigationText.Add("[blue]←[/] Previous ");
        if (currentPage < totalPages - 1)
            navigationText.Add(" Next [blue]→[/]");

        var footerContent = $"[dim]{string.Join(" • ", navigationText)}[/]\n";
        footerContent += $"[dim]Project: {this._project.ProjectPath}[/]\n";
        footerContent += $"[dim]Total sessions: {this._project.Sessions.Count}[/]";

        return new Markup(footerContent);
    }

    private static NavigationAction ShowSessionNavigation(ConsoleKeyInfo keyInfo, int currentPage, int totalPages)
    {
        return keyInfo.Key switch
        {
            ConsoleKey.UpArrow => NavigationAction.SelectUp,
            ConsoleKey.DownArrow => NavigationAction.SelectDown,
            ConsoleKey.Enter => NavigationAction.SelectItem,
            ConsoleKey.RightArrow when currentPage < totalPages - 1 => NavigationAction.NextPage,
            ConsoleKey.LeftArrow when currentPage > 0 => NavigationAction.PreviousPage,
            ConsoleKey.N => NavigationAction.NewSession,
            _ => NavigationAction.None, // Let global handler handle B/Escape
        };
    }

    private async Task HandleSessionNavigationResult(
        NavigationAction action,
        IReadOnlyList<ClaudeSessionMetadata> sortedSessions)
    {
        var currentPage = this._currentPage;
        var selectedIndex = this._selectedIndex;

        var itemsOnPage = Math.Min(PageSize, sortedSessions.Count - currentPage * PageSize);

        switch (action)
        {
            case NavigationAction.SelectUp:
            {
                this._selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : itemsOnPage - 1;
                break;
            }
            case NavigationAction.SelectDown:
            {
                this._selectedIndex = selectedIndex < itemsOnPage - 1 ? selectedIndex + 1 : 0;
                break;
            }
            case NavigationAction.SelectItem:
            {
                var actualIndex = currentPage * PageSize + selectedIndex;
                var selectedSession = sortedSessions[actualIndex];

                if (selectedSession.SessionId is null or "N/A")
                {
                    await this._toastContext.ShowWarningAsync("Unable to resume an unspecified session.");

                    break;
                }

                await this._navigationContext.NavigateToAsync<ClaudeCodePage>(selectedSession);

                break;
            }
            case NavigationAction.NewSession:
            {
                await this._navigationContext.NavigateToAsync<ClaudeCodePage>(this._project);

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
            case NavigationAction.Back:
            {
                await this._navigationContext.NavigateBackAsync();

                break;
            }
        }
    }

    public void Dispose()
    {
    }
}