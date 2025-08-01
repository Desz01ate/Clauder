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
        if (this._project.Sessions.Count == 0)
        {
            var emptyLayout = this.CreateEmptySessionsLayout();

            await AnsiConsole.Live(emptyLayout)
                             .StartAsync(async ctx =>
                             {
                                 var key = Console.ReadKey(true).Key;

                                 switch (key)
                                 {
                                     case ConsoleKey.N:
                                         await LaunchNewSessionAsync(this._project);
                                         break;
                                 }
                             });

            await this.PushBackAsync();
            return;
        }

        var sortedSessions = this._project.Sessions.OrderByDescending(s => s.Timestamp).ToList();
        var sessionCount = sortedSessions.Count;
        var totalPages = (int)Math.Ceiling((double)sessionCount / PageSize);

        // Ensure current page and selection are within bounds
        this._currentPage = Math.Min(this._currentPage, totalPages - 1);
        var itemsOnCurrentPage = Math.Min(PageSize, sessionCount - this._currentPage * PageSize);
        this._selectedIndex = Math.Min(this._selectedIndex, itemsOnCurrentPage - 1);

        var layout = this.CreateInitialLayout();
        var shouldExit = false;
        var shouldLaunchSession = false;
        var shouldLaunchNewSession = false;
        ClaudeSessionMetadata? selectedSession = null;

        await AnsiConsole.Live(layout)
                         .StartAsync(ctx =>
                         {
                             while (!shouldExit && !shouldLaunchSession && !shouldLaunchNewSession)
                             {
                                 cancellationToken.ThrowIfCancellationRequested();

                                 // Update the display
                                 ctx.UpdateTarget(this.CreateCurrentLayout(sortedSessions, totalPages));

                                 var navigationResult = ShowSessionNavigation(this._currentPage, totalPages);
                                 var handleResult = this.HandleSessionNavigationResult(navigationResult, sortedSessions, this._currentPage, this._selectedIndex);

                                 if (handleResult.session != null)
                                 {
                                     selectedSession = handleResult.session;
                                     shouldLaunchSession = true;
                                 }
                                 else if (handleResult.isNewSession)
                                 {
                                     shouldLaunchNewSession = true;
                                 }
                                 else if (navigationResult == NavigationAction.Back)
                                 {
                                     shouldExit = true;
                                 }
                             }

                             return Task.CompletedTask;
                         });

        if (selectedSession != null)
        {
            await LaunchExistingSessionAsync(selectedSession);
        }
        else if (shouldLaunchNewSession)
        {
            await LaunchNewSessionAsync(this._project);
        }

        if (shouldExit)
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
                new Layout("Footer").Size(4)
            );

        return layout;
    }

    private Layout CreateEmptySessionsLayout()
    {
        var layout = this.CreateInitialLayout();

        layout["Header"].Update(new Rule($"[#CC785C]Sessions for {this._project.ProjectName}[/]") { Justification = Justify.Left });
        layout["Content"].Update(new Markup("[yellow]No sessions found for this project.[/]"));
        layout["Footer"].Update(new Markup("[dim][cyan]N[/] New Session • [red]B[/] Back[/]"));

        return layout;
    }

    private Layout CreateCurrentLayout(List<ClaudeSessionMetadata> sessions, int totalPages)
    {
        var layout = this.CreateInitialLayout();

        var pageRule = new Rule($"[#CC785C]Sessions for {this._project.ProjectName}[/] [dim]({this._currentPage + 1}/{totalPages})[/]")
        {
            Justification = Justify.Left,
        };

        layout["Header"].Update(pageRule);
        layout["Content"].Update(this.CreateSessionTable(sessions));
        layout["Footer"].Update(this.CreateSessionNavigationMarkup(this._currentPage, totalPages));

        return layout;
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

        return table;
    }

    private Markup CreateSessionNavigationMarkup(int currentPage, int totalPages)
    {
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

        var footerContent = $"[dim]{string.Join(" • ", navigationText)}[/]\n";
        footerContent += $"[dim]Project: {this._project.ProjectPath}[/]\n";
        footerContent += $"[dim]Total sessions: {this._project.Sessions.Count}[/]";

        return new Markup(footerContent);
    }

    private static NavigationAction ShowSessionNavigation(int currentPage, int totalPages)
    {
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