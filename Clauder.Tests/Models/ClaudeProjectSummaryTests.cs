using System.Text.Json;
using Clauder.Models;
using FluentAssertions;

namespace Clauder.Tests.Models;

public class ClaudeProjectSummaryTests : IDisposable
{
    private readonly string _tempDirectory;

    public ClaudeProjectSummaryTests()
    {
        this._tempDirectory = Path.Combine(Path.GetTempPath(), $"claude-project-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(this._tempDirectory);
    }

    [Fact]
    public void FromDirectory_WithEmptyDirectory_ShouldCreateSummaryWithZeroSessions()
    {
        var projectDir = Path.Combine(this._tempDirectory, "my-test-project");
        Directory.CreateDirectory(projectDir);

        var summary = ClaudeProjectSummary.FromDirectory(projectDir);

        summary.Should().NotBeNull();
        summary.ProjectDirectoryName.Should().Be("my-test-project");
        summary.ProjectPath.Should().Be("my/test/project");
        summary.ProjectName.Should().Be("project");
        summary.SessionCount.Should().Be(0);
        summary.LastSessionTime.Should().BeNull();
        summary.LastGitBranch.Should().BeNull();
    }

    [Fact]
    public void FromDirectory_WithSessionFiles_ShouldCountSessions()
    {
        var projectDir = Path.Combine(this._tempDirectory, "test-project");
        Directory.CreateDirectory(projectDir);

        this.CreateSessionFile(projectDir, "session1.jsonl", DateTime.UtcNow.AddDays(-2));
        this.CreateSessionFile(projectDir, "session2.jsonl", DateTime.UtcNow.AddDays(-1));
        this.CreateSessionFile(projectDir, "session3.jsonl", DateTime.UtcNow);

        var summary = ClaudeProjectSummary.FromDirectory(projectDir);

        summary.Should().NotBeNull();
        summary.SessionCount.Should().Be(3);
        summary.LastSessionTime.Should().NotBeNull();
        summary.LastSessionTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromHours(8)); // Account for timezone differences
    }

    [Fact]
    public void FromDirectory_WithValidSessionMetadata_ShouldExtractGitBranch()
    {
        var projectDir = Path.Combine(this._tempDirectory, "git-project");
        Directory.CreateDirectory(projectDir);

        var sessionMetadata = new ClaudeSessionMetadata
        {
            SessionId = "test-session",
            Cwd = "/path/to/project",
            GitBranch = "feature/test-branch",
            Timestamp = DateTime.UtcNow,
            Message = new Message { Role = "user", Content = "test" },
            Uuid = Guid.NewGuid().ToString(),
            UserType = "individual",
            Type = "conversation",
            IsMeta = false,
            IsSidechain = false,
            ParentUuid = null,
            Version = "1.0"
        };

        var sessionFile = Path.Combine(projectDir, "session.jsonl");
        File.WriteAllText(sessionFile, JsonSerializer.Serialize(sessionMetadata));

        var summary = ClaudeProjectSummary.FromDirectory(projectDir);

        summary.Should().NotBeNull();
        summary.LastGitBranch.Should().Be("feature/test-branch");
    }

    [Fact]
    public void FromDirectory_WithCorruptedSessionFile_ShouldHandleGracefully()
    {
        var projectDir = Path.Combine(this._tempDirectory, "corrupted-project");
        Directory.CreateDirectory(projectDir);

        var sessionFile = Path.Combine(projectDir, "corrupted.jsonl");
        File.WriteAllText(sessionFile, "invalid json content");

        var summary = ClaudeProjectSummary.FromDirectory(projectDir);

        summary.Should().NotBeNull();
        summary.SessionCount.Should().Be(1); // Count physical files (now consistent with session details page)
        summary.LastGitBranch.Should().BeNull(); // Should not crash
    }

    [Fact]
    public void FromDirectory_WithComplexProjectPath_ShouldParseCorrectly()
    {
        var projectDir = Path.Combine(this._tempDirectory, "my-complex-project-name-with-dashes");
        Directory.CreateDirectory(projectDir);

        var summary = ClaudeProjectSummary.FromDirectory(projectDir);

        summary.Should().NotBeNull();
        summary.ProjectDirectoryName.Should().Be("my-complex-project-name-with-dashes");
        summary.ProjectPath.Should().Be("my/complex/project/name/with/dashes");
        summary.ProjectName.Should().Be("dashes");
    }

    [Fact]
    public void FromDirectory_WithMultipleSessionFiles_ShouldFindMostRecent()
    {
        var projectDir = Path.Combine(this._tempDirectory, "multi-session-project");
        Directory.CreateDirectory(projectDir);

        var oldTime = DateTime.UtcNow.AddDays(-5);
        var recentTime = DateTime.UtcNow.AddDays(-1);
        var newestTime = DateTime.UtcNow;

        this.CreateSessionFile(projectDir, "old.jsonl", oldTime, "old-branch");
        this.CreateSessionFile(projectDir, "recent.jsonl", recentTime, "recent-branch");
        this.CreateSessionFile(projectDir, "newest.jsonl", newestTime, "newest-branch");

        var summary = ClaudeProjectSummary.FromDirectory(projectDir);

        summary.Should().NotBeNull();
        summary.SessionCount.Should().Be(3);
        summary.LastSessionTime.Should().BeCloseTo(newestTime, TimeSpan.FromHours(8)); // Account for timezone differences
        summary.LastGitBranch.Should().Be("newest-branch");
    }

    [Fact]
    public void FromDirectory_WithNonJsonlFiles_ShouldIgnoreOtherFiles()
    {
        var projectDir = Path.Combine(this._tempDirectory, "mixed-files-project");
        Directory.CreateDirectory(projectDir);

        this.CreateSessionFile(projectDir, "session.jsonl", DateTime.UtcNow);
        File.WriteAllText(Path.Combine(projectDir, "readme.txt"), "This is a readme");
        File.WriteAllText(Path.Combine(projectDir, "config.json"), "{}");

        var summary = ClaudeProjectSummary.FromDirectory(projectDir);

        summary.Should().NotBeNull();
        summary.SessionCount.Should().Be(1); // Only .jsonl files should be counted
    }

    private void CreateSessionFile(string directory, string fileName, DateTime? lastWriteTime = null, string? gitBranch = null)
    {
        var sessionMetadata = new ClaudeSessionMetadata
        {
            SessionId = Path.GetFileNameWithoutExtension(fileName),
            Cwd = "/test/path",
            GitBranch = gitBranch ?? "main",
            Timestamp = DateTime.UtcNow,
            Message = new Message { Role = "user", Content = "test" },
            Uuid = Guid.NewGuid().ToString(),
            UserType = "individual",
            Type = "conversation",
            IsMeta = false,
            IsSidechain = false,
            ParentUuid = null,
            Version = "1.0"
        };

        var filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, JsonSerializer.Serialize(sessionMetadata));

        if (lastWriteTime.HasValue)
        {
            File.SetLastWriteTime(filePath, lastWriteTime.Value);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempDirectory))
        {
            Directory.Delete(this._tempDirectory, recursive: true);
        }
    }
}