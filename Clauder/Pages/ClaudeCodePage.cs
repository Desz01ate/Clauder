namespace Clauder.Pages;

using System.Diagnostics;
using Clauder.Abstractions;
using Clauder.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

public sealed class ClaudeCodePage : IFullConsoleControlPage, IInputHandler
{
    private readonly ClaudeSessionMetadata? _session;
    private readonly ClaudeProjectInfo? _project;
    private readonly IConfigurationService _configurationService;
    private readonly IToastContext _toastContext;

    private Process? _claudeProcess;
    private bool _processStarted;
    private bool _processCompleted;

    private static IRenderable EmptyRenderable => new Markup("[dim]Claude Code is taking over the control[/]");

    public ClaudeCodePage(
        object parameter,
        IConfigurationService configurationService,
        IToastContext toastContext)
    {
        if (parameter is ClaudeProjectInfo project)
        {
            this._project = project;
        }
        else if (parameter is ClaudeSessionMetadata session)
        {
            this._session = session;
        }
        else
        {
            throw new ArgumentException("Invalid parameter type. Expected ClaudeProjectInfo or ClaudeSessionMetadata.", nameof(parameter));
        }

        this._configurationService = configurationService;
        this._toastContext = toastContext;
    }

    public string Title =>
        this._session != null
            ? $"Claude Session - {this._session.SessionId?[..8]}..."
            : $"New Claude Session - {this._project?.ProjectName}";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<IRenderable> RenderHeaderAsync()
    {
        return ValueTask.FromResult(EmptyRenderable);
    }

    public ValueTask<IRenderable> RenderBodyAsync()
    {
        return ValueTask.FromResult(EmptyRenderable);
    }

    public ValueTask<IRenderable> RenderFooterAsync()
    {
        return ValueTask.FromResult(EmptyRenderable);
    }

    public Task<bool> HandleInputAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await this.StartClaudeProcessAsync();
            this._processStarted = true;

            // Wait for the Claude process to complete
            if (this._claudeProcess != null)
            {
                await this._claudeProcess.WaitForExitAsync(cancellationToken);
                this._processCompleted = true;
            }
        }
        catch (Exception ex)
        {
            await this._toastContext.ShowErrorAsync($"Failed to start Claude: {ex.Message}");
        }
    }

    private async Task StartClaudeProcessAsync()
    {
        Console.Clear();

        var configuration = await this._configurationService.GetConfigurationAsync();

        ProcessStartInfo processStartInfo;

        if (this._session != null)
        {
            // Resume existing session
            if (string.IsNullOrWhiteSpace(this._session.SessionId) || string.IsNullOrWhiteSpace(this._session.Cwd))
            {
                throw new InvalidOperationException("Session ID or working directory is missing.");
            }

            processStartInfo = new ProcessStartInfo
            {
                FileName = configuration.ClaudeExecutablePath,
                Arguments = $"-r \"{this._session.SessionId}\"",
                WorkingDirectory = this._session.Cwd,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = false,
            };
        }
        else if (this._project != null)
        {
            // Start new session
            if (string.IsNullOrWhiteSpace(this._project.ProjectPath))
            {
                throw new InvalidOperationException("Project path is missing.");
            }

            processStartInfo = new ProcessStartInfo
            {
                FileName = configuration.ClaudeExecutablePath,
                Arguments = string.Empty,
                WorkingDirectory = this._project.ProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = false,
            };
        }
        else
        {
            throw new InvalidOperationException("Neither session nor project information provided.");
        }

        // Start the process - RunAsync will handle waiting for completion
        this._claudeProcess = Process.Start(processStartInfo);

        if (this._claudeProcess == null)
        {
            throw new InvalidOperationException("Failed to start Claude process.");
        }
    }

    public void Dispose()
    {
        try
        {
            if (this._claudeProcess != null && !this._claudeProcess.HasExited)
            {
                this._claudeProcess.Kill();
            }

            this._claudeProcess?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}