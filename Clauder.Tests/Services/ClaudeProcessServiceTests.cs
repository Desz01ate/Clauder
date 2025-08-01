using Clauder.Models;
using Clauder.Services;
using FluentAssertions;
using Moq;

namespace Clauder.Tests.Services;

using Abstractions;

public class ClaudeProcessServiceTests
{
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private readonly ClaudeProcessService _processService;
    private readonly ClaudeConfiguration _testConfiguration;

    public ClaudeProcessServiceTests()
    {
        this._mockConfigurationService = new Mock<IConfigurationService>();
        this._testConfiguration = new ClaudeConfiguration
        {
            ClaudeExecutablePath = "/usr/local/bin/claude",
            ClaudeProjectDirectory = "/home/user/.claude/projects",
            ClaudeRootDirectory = "/home/user/.claude"
        };

        this._mockConfigurationService.Setup(x => x.GetConfiguration())
            .Returns(this._testConfiguration);
        this._mockConfigurationService.Setup(x => x.GetConfigurationAsync())
            .ReturnsAsync(this._testConfiguration);

        this._processService = new ClaudeProcessService(this._mockConfigurationService.Object);
    }

    [Fact]
    public void Constructor_WithValidConfigurationService_ShouldNotThrow()
    {
        var act = () => new ClaudeProcessService(this._mockConfigurationService.Object);
        
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ShouldAcceptConfigurationService()
    {
        var service = new ClaudeProcessService(this._mockConfigurationService.Object);
        
        service.Should().NotBeNull();
        service.Should().BeOfType<ClaudeProcessService>();
    }
}