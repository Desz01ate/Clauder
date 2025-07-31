namespace Clauder.UI;

using System.Diagnostics;
using Models;

public class ClaudeSessionDisplay
{
    public async Task LaunchExistingSessionAsync(ClaudeSessionMetadata session)
    {
        if (string.IsNullOrEmpty(session.SessionId) || string.IsNullOrEmpty(session.Cwd))
        {
            ProjectDisplayService.DisplayErrorMessage("Session ID or working directory is missing. Cannot launch session.");
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
            ProjectDisplayService.DisplayErrorMessage($"Error launching Claude session: {ex.Message}");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    public async Task LaunchNewSessionAsync(ClaudeProjectInfo project)
    {
        if (string.IsNullOrEmpty(project.ProjectPath))
        {
            ProjectDisplayService.DisplayErrorMessage("Project path is missing. Cannot launch new session.");
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
            ProjectDisplayService.DisplayErrorMessage($"Error launching new Claude session: {ex.Message}");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}