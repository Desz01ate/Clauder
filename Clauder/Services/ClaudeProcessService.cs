using System.Diagnostics;
using Clauder.Abstractions;
using Clauder.Models;
using Spectre.Console;

namespace Clauder.Services;

public class ClaudeProcessService : IClaudeProcessService
{
    private readonly IConfigurationService _configurationService;

    public ClaudeProcessService(IConfigurationService configurationService)
    {
        this._configurationService = configurationService;
    }

    public async Task LaunchExistingSessionAsync(ClaudeSessionMetadata session)
    {
        if (string.IsNullOrWhiteSpace(session.SessionId) || string.IsNullOrWhiteSpace(session.Cwd))
        {
            AnsiConsole.MarkupLine("[red]Session ID or working directory is missing. Cannot launch session.[/]");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }

        try
        {
            var configuration = this._configurationService.GetConfiguration();
            var processStartInfo = new ProcessStartInfo
            {
                FileName = configuration.ClaudeExecutablePath,
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
            Console.WriteLine($"Command: {configuration.ClaudeExecutablePath} -r \"{session.SessionId}\"");
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

    public async Task LaunchNewSessionAsync(ClaudeProjectInfo project)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectPath))
        {
            AnsiConsole.MarkupLine("[red]Project path is missing. Cannot launch new session.[/]");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }

        try
        {
            var configuration = await this._configurationService.GetConfigurationAsync();
            var processStartInfo = new ProcessStartInfo
            {
                FileName = configuration.ClaudeExecutablePath,
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
            Console.WriteLine($"Command: {configuration.ClaudeExecutablePath}");
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
}