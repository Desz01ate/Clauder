namespace Clauder.Models;

public class ClaudeProjectSummary
{
    public required string ProjectName { get; init; }

    public required string ProjectPath { get; init; }

    public required string ProjectDirectoryName { get; init; }

    public required int SessionCount { get; init; }

    public DateTime? LastSessionTime { get; init; }

    public string? LastGitBranch { get; init; }

    public static ClaudeProjectSummary FromDirectory(string projectDirectory)
    {
        var directoryName = Path.GetFileName(projectDirectory);
        var projectPath = directoryName.Replace('-', Path.DirectorySeparatorChar);
        var projectName = Path.GetFileName(projectPath);

        var sessionFiles = Directory.GetFiles(projectDirectory, "*.jsonl");
        var sessionCount = sessionFiles.Length;

        DateTime? lastSessionTime = null;
        string? lastGitBranch = null;

        if (sessionFiles.Length > 0)
        {
            var mostRecentFile = sessionFiles
                                 .Select(f => new { File = f, Info = new FileInfo(f) })
                                 .MaxBy(x => x.Info.LastWriteTime)!;

            lastSessionTime = mostRecentFile.Info.LastWriteTime;

            lastGitBranch = TryGetGitBranchFromFirstLine(mostRecentFile.File);
        }

        return new ClaudeProjectSummary
        {
            ProjectName = projectName,
            ProjectPath = projectPath,
            ProjectDirectoryName = directoryName,
            SessionCount = sessionCount,
            LastSessionTime = lastSessionTime,
            LastGitBranch = lastGitBranch
        };
    }

    private static string? TryGetGitBranchFromFirstLine(string sessionFile)
    {
        try
        {
            var firstLine = File.ReadLines(sessionFile).FirstOrDefault();

            if (firstLine != null)
            {
                var metadata = System.Text.Json.JsonSerializer.Deserialize<ClaudeSessionMetadata>(firstLine);
                return metadata?.GitBranch;
            }
        }
        catch
        {
        }

        return null;
    }
}