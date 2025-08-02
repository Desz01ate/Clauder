namespace Clauder.Models;

public class ClaudeProjectInfo
{
    private readonly IReadOnlyList<ClaudeSessionMetadata> _sessions;

    public ClaudeProjectInfo(IEnumerable<ClaudeSessionMetadata> sessions)
    {
        this._sessions = sessions.ToList();
    }

    public string ProjectName { get; init; }

    public string ProjectPath { get; init; }

    public IReadOnlyList<ClaudeSessionMetadata> Sessions => this._sessions;

    public static ClaudeProjectInfo From(List<ClaudeSessionMetadata> sessions)
    {
        var path = sessions.First().Cwd!;
        var projectName = path.Split(Path.DirectorySeparatorChar).Last();

        return new ClaudeProjectInfo(sessions)
        {
            ProjectName = projectName,
            ProjectPath = path,
        };
    }
}