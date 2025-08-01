namespace Clauder.Pages;

using System.Diagnostics;
using Clauder.Abstractions;
using Clauder.Enums;
using Clauder.Models;
using Spectre.Console;

public sealed class SessionsPage : IDisplay
{
    private readonly ClaudeProjectInfo _project;
    private readonly INavigationService _navigationService;

    private const int PageSize = 10;
    private int _currentPage;
    private int _selectedIndex;

    public SessionsPage(ClaudeProjectInfo project, INavigationService navigationService)
    {
        this._project = project;
        this._navigationService = navigationService;
    }

    public string Title => $"[#CC785C]Sessions - {this._project.ProjectName}[/]";

    public async Task DisplayAsync(CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine();
        
        var rule = new Rule($"[#CC785C]Sessions for {this._project.ProjectName}[/]")
        {
            Justification = Justify.Left,
        };

        AnsiConsole.Write(rule);

        AnsiConsole.WriteLine();

        if (this._project.Sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No sessions found for this project.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim][cyan]N[/] New Session • [red]B[/] Back[/]");
            var key = Console.ReadKey(true).Key;

            switch (key)
            {
                case ConsoleKey.N:
                    await LaunchNewSessionAsync(this._project);
                    break;
                default:
                    await this.PushBackAsync();
                    return;
            }

            return;
        }

        var sortedSessions = this._project.Sessions.OrderByDescending(s => s.Timestamp).ToList();
        var sessionCount = sortedSessions.Count;
        var totalPages = (int)Math.Ceiling((double)sessionCount / PageSize);

        // Ensure current page and selection are within bounds
        this._currentPage = Math.Min(this._currentPage, totalPages - 1);
        var itemsOnCurrentPage = Math.Min(PageSize, sessionCount - this._currentPage * PageSize);
        this._selectedIndex = Math.Min(this._selectedIndex, itemsOnCurrentPage - 1);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnsiConsole.Clear();
            AnsiConsole.WriteLine();

            DisplaySessionPageWithSelection(this._project, sortedSessions, this._currentPage, totalPages, this._selectedIndex);

            if (totalPages <= 1 && sessionCount <= PageSize)
            {
                var result = ShowSessionNavigation(this._currentPage, totalPages);
                var singlePageResult = this.HandleSessionNavigationResult(result, sortedSessions, this._currentPage, this._selectedIndex);

                if (singlePageResult.session != null)
                {
                    await LaunchExistingSessionAsync(singlePageResult.session);
                }
                else if (singlePageResult.isNewSession)
                {
                    await LaunchNewSessionAsync(this._project);
                }
                else if (result == NavigationAction.Back)
                {
                    await this.PushBackAsync();
                    return;
                }
            }
            else
            {
                var navigationResult = ShowSessionNavigation(this._currentPage, totalPages);
                var handleResult = this.HandleSessionNavigationResult(navigationResult, sortedSessions, this._currentPage, this._selectedIndex);

                if (handleResult.session != null)
                {
                    await LaunchExistingSessionAsync(handleResult.session);
                }
                else if (handleResult.isNewSession)
                {
                    await LaunchNewSessionAsync(this._project);
                }
                else if (navigationResult == NavigationAction.Back)
                {
                    await this.PushBackAsync();
                    return;
                }
            }
        }
    }

    public async Task PushBackAsync()
    {
        await this._navigationService.NavigateBackAsync();
    }

    private static void DisplaySessionPageWithSelection(ClaudeProjectInfo project, List<ClaudeSessionMetadata> sessions, int currentPage, int totalPages, int selectedIndex)
    {
        var pageRule = new Rule($"[#CC785C]Sessions for {project.ProjectName}[/] [dim]({currentPage + 1}/{totalPages})[/]")
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

        var pageSessions = sessions.Skip(currentPage * PageSize).Take(PageSize);

        var i = 0;
        foreach (var session in pageSessions)
        {
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
            i++;
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Project: {project.ProjectPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Total sessions: {project.Sessions.Count}[/]");
    }

    private static NavigationAction ShowSessionNavigation(int currentPage, int totalPages)
    {
        AnsiConsole.WriteLine();
        var navigationItems = new[]
        {
            "[green]↑↓[/] Select",
            "[green]Enter[/] View Session",
            "[cyan]N[/] New Session",
        };
        var navigationText = new List<string>(navigationItems);

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

    private static async Task LaunchExistingSessionAsync(ClaudeSessionMetadata session)
    {
        if (string.IsNullOrEmpty(session.SessionId) || string.IsNullOrEmpty(session.Cwd))
        {
            AnsiConsole.MarkupLine("[red]Session ID or working directory is missing. Cannot launch session.[/]");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = $"-r \"{session.SessionId}\"",
                WorkingDirectory = session.Cwd,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = false,
            };

            Console.Clear();
            Console.WriteLine($"Launching Claude session: {session.SessionId}");
            Console.WriteLine($"Working directory: {session.Cwd}");
            Console.WriteLine($"Command: claude -r \"{session.SessionId}\"");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to terminate the session...");
            Console.WriteLine(new string('=', 50));

            using var process = Process.Start(processStartInfo);

            if (process == null)
            {
                throw new Exception("Failed to start Claude process.");
            }

            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error launching Claude session: {ex.Message}[/]");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    private static async Task LaunchNewSessionAsync(ClaudeProjectInfo project)
    {
        if (string.IsNullOrEmpty(project.ProjectPath))
        {
            AnsiConsole.MarkupLine("[red]Project path is missing. Cannot launch new session.[/]");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = string.Empty,
                WorkingDirectory = project.ProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = false,
            };

            Console.Clear();
            Console.WriteLine($"Starting new Claude session for project: {project.ProjectName}");
            Console.WriteLine($"Working directory: {project.ProjectPath}");
            Console.WriteLine("Command: claude");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to terminate the session...");
            Console.WriteLine(new string('=', 50));

            using var process = Process.Start(processStartInfo);

            if (process == null)
            {
                throw new Exception("Failed to start Claude process.");
            }

            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error launching new Claude session: {ex.Message}[/]");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    public void Dispose()
    {
    }
}