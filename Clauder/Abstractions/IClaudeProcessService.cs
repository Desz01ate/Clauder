using Clauder.Models;

namespace Clauder.Abstractions;

public interface IClaudeProcessService
{
    Task LaunchExistingSessionAsync(ClaudeSessionMetadata session);

    Task LaunchNewSessionAsync(ClaudeProjectInfo project);
}