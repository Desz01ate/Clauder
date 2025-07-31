namespace Clauder.Models;

public class ClaudeProjectInfo
{
    private readonly List<ClaudeSessionMetadata> _sessions;

    private ClaudeProjectInfo(IEnumerable<ClaudeSessionMetadata> session)
    {
        this._sessions = session.ToList();
    }

    public string ProjectName { get; init; }

    public string ProjectPath { get; init; }

    public IReadOnlyCollection<ClaudeSessionMetadata> Sessions => this._sessions;

    public static ClaudeProjectInfo From(IGrouping<string, ClaudeSessionMetadata> group)
    {
        var path = group.Key;
        var projectName = path.Split(Path.DirectorySeparatorChar).Last();

        return new ClaudeProjectInfo(group)
        {
            ProjectName = projectName,
            ProjectPath = path,
        };
    }
}