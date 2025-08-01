using Clauder.Services;
using FluentAssertions;

namespace Clauder.Tests.Services;

public class SimpleServiceTests
{
    [Fact]
    public void ConfigurationService_Constructor_ShouldNotThrow()
    {
        var act = () => new ConfigurationService();
        
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ConfigurationService_GetConfigurationAsync_ShouldReturnValidConfig()
    {
        var service = new ConfigurationService();
        
        var config = await service.GetConfigurationAsync();
        
        config.Should().NotBeNull();
        config.ClaudeExecutablePath.Should().NotBeNullOrEmpty();
        config.ClaudeProjectDirectory.Should().NotBeNullOrEmpty();
        config.ClaudeRootDirectory.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ClaudeProcessService_Constructor_ShouldNotThrow()
    {
        var configService = new ConfigurationService();
        
        var act = () => new ClaudeProcessService(configService);
        
        act.Should().NotThrow();
    }
}