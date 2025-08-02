namespace Clauder.Models;

using System.Text.Json;

public sealed class ClaudeProjectSummary
{
    public required string ProjectName { get; init; }

    public required string ProjectPath { get; init; }

    public required string ProjectDirectoryName { get; init; }

    public required int SessionCount { get; init; }

    public DateTime? LastSessionTime { get; init; }

    public string? LastGitBranch { get; init; }

    public string[] SessionFiles { get; init; }

    public static ClaudeProjectSummary FromDirectory(string projectDirectory)
    {
        var directoryName = Path.GetFileName(projectDirectory);
        var sessionFiles = Directory.GetFiles(projectDirectory, "*.jsonl");
        var sessionCount = sessionFiles.Length;

        DateTime? lastSessionTime = null;
        string? lastGitBranch = null;
        string? projectPath = null;
        string? projectName = null;

        if (sessionFiles.Length > 0)
        {
            var buffer =
                sessionFiles
                    .Select(f => new { File = f, Info = new FileInfo(f) })
                    .ToArray();
            var mostRecentFile = buffer.MaxBy(x => x.Info.LastWriteTime)!;
            var largestFile = buffer.MaxBy(x => x.Info.Length)!;

            lastSessionTime = mostRecentFile.Info.LastWriteTime;

            var metadata = GetMetadata(largestFile.File);

            lastGitBranch = metadata?.GitBranch;

            if (metadata?.Cwd != null)
            {
                projectPath = metadata.Cwd;
                projectName = Path.GetFileName(projectPath);
            }
        }

        // Fallback to directory name if no Cwd found in metadata
        if (projectPath == null)
        {
            projectPath = directoryName.Replace('-', Path.DirectorySeparatorChar);
            projectName = Path.GetFileName(projectPath);
        }

        return new ClaudeProjectSummary
        {
            ProjectName = projectName!,
            ProjectPath = projectPath,
            ProjectDirectoryName = directoryName,
            SessionCount = sessionCount,
            LastSessionTime = lastSessionTime,
            LastGitBranch = lastGitBranch,
            SessionFiles = sessionFiles,
        };
    }

    private static ClaudeSessionMetadata? GetMetadata(string sessionFile)
    {
        try
        {
            // Read first few lines to find metadata with Cwd property
            var lines = File.ReadLines(sessionFile).Take(5);

            foreach (var line in lines)
            {
                var metadata = JsonSerializer.Deserialize<ClaudeSessionMetadata>(line);

                if (metadata?.Cwd != null)
                {
                    return metadata;
                }
            }
        }
        catch
        {
        }

        return null;
    }
}