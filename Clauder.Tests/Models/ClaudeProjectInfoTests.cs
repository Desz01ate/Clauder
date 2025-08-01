using Clauder.Models;
using FluentAssertions;

namespace Clauder.Tests.Models;

public class ClaudeProjectInfoTests
{
    [Fact]
    public void Constructor_WithEmptySessionList_ShouldCreateValidInstance()
    {
        var sessions = new List<ClaudeSessionMetadata>();

        var projectInfo = new ClaudeProjectInfo(sessions)
        {
            ProjectName = "TestProject",
            ProjectPath = "/path/to/project"
        };

        projectInfo.Should().NotBeNull();
        projectInfo.Sessions.Should().BeEmpty();
        projectInfo.ProjectName.Should().Be("TestProject");
        projectInfo.ProjectPath.Should().Be("/path/to/project");
    }

    [Fact]
    public void Constructor_WithSessions_ShouldStoreSessionsCorrectly()
    {
        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("session1", "/project/path"),
            CreateTestSession("session2", "/project/path"),
            CreateTestSession("session3", "/project/path")
        };

        var projectInfo = new ClaudeProjectInfo(sessions)
        {
            ProjectName = "TestProject",
            ProjectPath = "/project/path"
        };

        projectInfo.Sessions.Should().HaveCount(3);
        projectInfo.Sessions.Should().Contain(s => s.SessionId == "session1");
        projectInfo.Sessions.Should().Contain(s => s.SessionId == "session2");
        projectInfo.Sessions.Should().Contain(s => s.SessionId == "session3");
    }

    [Fact]
    public void Sessions_ShouldBeReadOnly()
    {
        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("session1", "/project/path")
        };

        var projectInfo = new ClaudeProjectInfo(sessions)
        {
            ProjectName = "TestProject",
            ProjectPath = "/project/path"
        };

        projectInfo.Sessions.Should().BeAssignableTo<IReadOnlyList<ClaudeSessionMetadata>>();
    }

    [Fact]
    public void From_WithValidGrouping_ShouldCreateProjectInfo()
    {
        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("session1", "/path/to/my/project"),
            CreateTestSession("session2", "/path/to/my/project"),
            CreateTestSession("session3", "/path/to/my/project")
        };

        var grouping = sessions.GroupBy(s => s.Cwd!).First();

        var projectInfo = ClaudeProjectInfo.From(grouping);

        projectInfo.Should().NotBeNull();
        projectInfo.ProjectPath.Should().Be("/path/to/my/project");
        projectInfo.ProjectName.Should().Be("project");
        projectInfo.Sessions.Should().HaveCount(3);
    }

    [Fact]
    public void From_WithSimpleProjectPath_ShouldParseProjectNameCorrectly()
    {
        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("session1", "/simple")
        };

        var grouping = sessions.GroupBy(s => s.Cwd!).First();

        var projectInfo = ClaudeProjectInfo.From(grouping);

        projectInfo.ProjectName.Should().Be("simple");
        projectInfo.ProjectPath.Should().Be("/simple");
    }

    [Fact]
    public void From_WithComplexProjectPath_ShouldParseProjectNameCorrectly()
    {
        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("session1", "/home/user/projects/my-awesome-project")
        };

        var grouping = sessions.GroupBy(s => s.Cwd!).First();

        var projectInfo = ClaudeProjectInfo.From(grouping);

        projectInfo.ProjectName.Should().Be("my-awesome-project");
        projectInfo.ProjectPath.Should().Be("/home/user/projects/my-awesome-project");
    }

    [Fact]
    public void From_WithTrailingSlash_ShouldHandleCorrectly()
    {
        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("session1", "/path/to/project/")
        };

        var grouping = sessions.GroupBy(s => s.Cwd!).First();

        var projectInfo = ClaudeProjectInfo.From(grouping);

        projectInfo.ProjectName.Should().Be("");
        projectInfo.ProjectPath.Should().Be("/path/to/project/");
    }

    [Fact]
    public void From_WithWindowsPath_ShouldHandleCorrectly()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Skip test on non-Windows platforms
        }


        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("session1", @"C:\Users\User\Projects\MyProject")
        };

        var grouping = sessions.GroupBy(s => s.Cwd!).First();

        var projectInfo = ClaudeProjectInfo.From(grouping);

        projectInfo.ProjectName.Should().Be("MyProject");
        projectInfo.ProjectPath.Should().Be(@"C:\Users\User\Projects\MyProject");
    }

    [Fact]
    public void From_WithEmptyGrouping_ShouldCreateEmptyProjectInfo()
    {
        var sessions = new List<ClaudeSessionMetadata>
        {
            CreateTestSession("dummy", "/empty")
        };
        var grouping = sessions.GroupBy(s => s.Cwd ?? "/empty").First();

        var projectInfo = ClaudeProjectInfo.From(grouping);

        projectInfo.Should().NotBeNull();
        projectInfo.Sessions.Should().HaveCount(1);
        projectInfo.ProjectPath.Should().Be("/empty");
        projectInfo.ProjectName.Should().Be("empty");
    }

    [Fact]
    public void From_PreservesOriginalSessionOrder()
    {
        var session1 = CreateTestSession("session1", "/project", DateTime.UtcNow.AddDays(-2));
        var session2 = CreateTestSession("session2", "/project", DateTime.UtcNow.AddDays(-1));
        var session3 = CreateTestSession("session3", "/project", DateTime.UtcNow);

        var sessions = new List<ClaudeSessionMetadata> { session1, session2, session3 };
        var grouping = sessions.GroupBy(s => s.Cwd!).First();

        var projectInfo = ClaudeProjectInfo.From(grouping);

        projectInfo.Sessions.Should().ContainInOrder(session1, session2, session3);
    }

    private static ClaudeSessionMetadata CreateTestSession(string sessionId, string cwd, DateTime? timestamp = null)
    {
        return new ClaudeSessionMetadata
        {
            SessionId = sessionId,
            Cwd = cwd,
            Timestamp = timestamp ?? DateTime.UtcNow,
            GitBranch = "main",
            Message = new Message { Role = "user", Content = "test" },
            Uuid = Guid.NewGuid().ToString(),
            UserType = "individual",
            Type = "conversation",
            IsMeta = false,
            IsSidechain = false,
            ParentUuid = null,
            Version = "1.0"
        };
    }
}