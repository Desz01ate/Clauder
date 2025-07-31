using Clauder.Models;
using Spectre.Console;

namespace Clauder.UI;

using Enums;

public class SessionDisplayService
{
    private const int PageSize = 10;
    private int _currentPage;
    private int _selectedIndex;

    public (ClaudeSessionMetadata? session, bool isNewSession) DisplaySessionsPaginated(ClaudeProjectInfo project)
    {
        var rule = new Rule($"[bold blue]Sessions for {project.ProjectName}[/]")
        {
            Justification = Justify.Left,
        };

        AnsiConsole.Write(rule);

        if (project.Sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No sessions found for this project.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim][cyan]N[/] New Session • [red]B[/] Back[/]");
            var key = Console.ReadKey(true).Key;
            return key switch
            {
                ConsoleKey.N => (null, true),
                _ => (null, false)
            };
        }

        var sortedSessions = project.Sessions.OrderByDescending(s => s.Timestamp).ToList();
        var totalPages = (int)Math.Ceiling((double)sortedSessions.Count / PageSize);

        // Ensure current page and selection are within bounds
        this._currentPage = Math.Min(this._currentPage, totalPages - 1);
        var itemsOnCurrentPage = Math.Min(PageSize, sortedSessions.Count - this._currentPage * PageSize);
        this._selectedIndex = Math.Min(this._selectedIndex, itemsOnCurrentPage - 1);

        while (true)
        {
            AnsiConsole.Clear();

            AnsiConsole.WriteLine();

            DisplaySessionPageWithSelection(project, sortedSessions, this._currentPage, totalPages, this._selectedIndex);

            if (totalPages <= 1 && sortedSessions.Count <= PageSize)
            {
                var result = ShowSessionNavigation(this._currentPage, totalPages);
                var singlePageResult = this.HandleSessionNavigationResult(result, sortedSessions, this._currentPage, this._selectedIndex);
                if (singlePageResult.session != null || result == NavigationAction.Back || result == NavigationAction.NewSession)
                    return singlePageResult;
            }
            else
            {
                var navigationResult = ShowSessionNavigation(this._currentPage, totalPages);
                var handleResult = this.HandleSessionNavigationResult(navigationResult, sortedSessions, this._currentPage, this._selectedIndex);

                if (handleResult.session != null || navigationResult == NavigationAction.Back || navigationResult == NavigationAction.NewSession)
                    return handleResult;
            }
        }
    }

    private static void DisplaySessionPageWithSelection(ClaudeProjectInfo project, List<ClaudeSessionMetadata> sessions, int currentPage, int totalPages, int selectedIndex)
    {
        var pageRule = new Rule($"[bold blue]Sessions for {project.ProjectName}[/] [dim]({currentPage + 1}/{totalPages})[/]")
        {
            Justification = Justify.Left,
        };

        AnsiConsole.Write(pageRule);

        var table = new Table();
        table.AddColumn("[bold]#[/]");
        table.AddColumn("[bold]Session ID[/]");
        table.AddColumn("[bold]Timestamp[/]");
        table.AddColumn("[bold]Git Branch[/]");
        table.AddColumn("[bold]Type[/]");
        table.AddColumn("[bold]Message Preview[/]");

        var pageSessions = sessions.Skip(currentPage * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < pageSessions.Count; i++)
        {
            var session = pageSessions[i];
            var sessionIdShort = session.SessionId?.Length > 8 ? session.SessionId[..8] + "..." : session.SessionId ?? "N/A";
            var messagePreview = session.Message?.Content?.Length > 50
                ? session.Message.Content[..50] + "..."
                : session.Message?.Content ?? "N/A";

            var isSelected = i == selectedIndex;
            var selectionMarker = isSelected ? "[yellow]>[/]" : " ";
            var sessionId = isSelected ? $"[yellow]{sessionIdShort}[/]" : $"[dim]{sessionIdShort}[/]";

            table.AddRow(
                selectionMarker,
                sessionId,
                $"[dim]{session.Timestamp:yyyy-MM-dd HH:mm:ss}[/]",
                $"[dim]{session.GitBranch ?? "N/A"}[/]",
                $"[dim]{session.Type ?? "N/A"}[/]",
                $"[dim]{messagePreview}[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Project: {project.ProjectPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Total sessions: {sessions.Count}[/]");
    }

    private static NavigationAction ShowSessionNavigation(int currentPage, int totalPages)
    {
        AnsiConsole.WriteLine();
        var navigationText = new List<string>
        {
            "[green]↑↓[/] Select",
            "[green]Enter[/] View Session",
            "[cyan]N[/] New Session",
        };

        if (currentPage > 0)
            navigationText.Add("[blue]←[/] Previous ");
        if (currentPage < totalPages - 1)
            navigationText.Add(" Next [blue]→[/]");
        navigationText.Add("[red]B[/] Back");

        AnsiConsole.MarkupLine($"[dim]{string.Join(" • ", navigationText)}[/]");

        var key = Console.ReadKey(true).Key;

        return key switch
        {
            ConsoleKey.UpArrow => NavigationAction.SelectUp,
            ConsoleKey.DownArrow => NavigationAction.SelectDown,
            ConsoleKey.Enter => NavigationAction.SelectItem,
            ConsoleKey.RightArrow when currentPage < totalPages - 1 => NavigationAction.NextPage,
            ConsoleKey.LeftArrow when currentPage > 0 => NavigationAction.PreviousPage,
            ConsoleKey.N => NavigationAction.NewSession,
            ConsoleKey.B => NavigationAction.Back,
            ConsoleKey.Escape => NavigationAction.Back,
            _ => NavigationAction.None,
        };
    }

    private (ClaudeSessionMetadata? session, bool isNewSession) HandleSessionNavigationResult(
        NavigationAction action,
        List<ClaudeSessionMetadata> sessions,
        int currentPage,
        int selectedIndex)
    {
        var itemsOnPage = Math.Min(PageSize, sessions.Count - currentPage * PageSize);

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
                return (sessions[actualIndex], false);
            case NavigationAction.NextPage:
                this._currentPage++;
                this._selectedIndex = Math.Clamp(this._selectedIndex, 0, itemsOnPage - 1);
                break;
            case NavigationAction.PreviousPage:
                this._currentPage--;
                this._selectedIndex = Math.Clamp(this._selectedIndex, 0, itemsOnPage - 1);
                break;
            case NavigationAction.NewSession:
                return (null, true);
            case NavigationAction.Back:
                break;
        }

        return (null, false);
    }
}