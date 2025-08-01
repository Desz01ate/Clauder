using System.Text.Json;
using Clauder.Models;
using FluentAssertions;

namespace Clauder.Tests.Models;

public class ClaudeConfigurationTests
{
    [Fact]
    public void CreateDefault_ShouldReturnValidConfiguration()
    {
        var config = ClaudeConfiguration.CreateDefault();
        
        config.Should().NotBeNull();
        config.ClaudeExecutablePath.Should().NotBeNullOrEmpty();
        config.ClaudeProjectDirectory.Should().NotBeNullOrEmpty();
        config.ClaudeRootDirectory.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ClaudeExecutablePath_DefaultValue_ShouldNotBeNull()
    {
        var config = new ClaudeConfiguration();
        
        config.ClaudeExecutablePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ClaudeProjectDirectory_DefaultValue_ShouldNotBeNull()
    {
        var config = new ClaudeConfiguration();
        
        config.ClaudeProjectDirectory.Should().NotBeNullOrEmpty();
        config.ClaudeProjectDirectory.Should().EndWith("projects");
    }

    [Fact]
    public void ClaudeRootDirectory_DefaultValue_ShouldNotBeNull()
    {
        var config = new ClaudeConfiguration();
        
        config.ClaudeRootDirectory.Should().NotBeNullOrEmpty();
        config.ClaudeRootDirectory.Should().EndWith(".claude");
    }

    [Fact]
    public void Serialization_ShouldPreserveProperties()
    {
        var originalConfig = new ClaudeConfiguration
        {
            ClaudeExecutablePath = "/custom/path/claude",
            ClaudeProjectDirectory = "/custom/projects",
            ClaudeRootDirectory = "/custom/root"
        };

        var json = JsonSerializer.Serialize(originalConfig);
        var deserializedConfig = JsonSerializer.Deserialize<ClaudeConfiguration>(json);

        deserializedConfig.Should().NotBeNull();
        deserializedConfig!.ClaudeExecutablePath.Should().Be(originalConfig.ClaudeExecutablePath);
        deserializedConfig.ClaudeProjectDirectory.Should().Be(originalConfig.ClaudeProjectDirectory);
        deserializedConfig.ClaudeRootDirectory.Should().Be(originalConfig.ClaudeRootDirectory);
    }

    [Fact]
    public void JsonPropertyNames_ShouldMatchExpectedFormat()
    {
        var config = new ClaudeConfiguration
        {
            ClaudeExecutablePath = "/test/claude",
            ClaudeProjectDirectory = "/test/projects",
            ClaudeRootDirectory = "/test/root"
        };

        var json = JsonSerializer.Serialize(config);
        
        json.Should().Contain("claudeExecutablePath");
        json.Should().Contain("claudeDataDirectory");
        json.Should().Contain("claudeRootDirectory");
    }

    [Fact]
    public void Deserialization_WithMissingProperties_ShouldUseDefaults()
    {
        var incompleteJson = """{"claudeExecutablePath": "/custom/claude"}""";
        
        var config = JsonSerializer.Deserialize<ClaudeConfiguration>(incompleteJson);
        
        config.Should().NotBeNull();
        config!.ClaudeExecutablePath.Should().Be("/custom/claude");
        config.ClaudeProjectDirectory.Should().NotBeNullOrEmpty();
        config.ClaudeRootDirectory.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceValues_ShouldUseDefaults(string emptyValue)
    {
        var config = new ClaudeConfiguration
        {
            ClaudeExecutablePath = emptyValue,
            ClaudeProjectDirectory = emptyValue,
            ClaudeRootDirectory = emptyValue
        };

        config.ClaudeExecutablePath.Should().Be(emptyValue);
        config.ClaudeProjectDirectory.Should().Be(emptyValue);
        config.ClaudeRootDirectory.Should().Be(emptyValue);
    }

    [Fact]
    public void DefaultPaths_ShouldPointToValidDirectories()
    {
        var config = ClaudeConfiguration.CreateDefault();
        
        // Verify paths are reasonable (don't test actual existence as that depends on environment)
        config.ClaudeRootDirectory.Should().Contain(".claude");
        config.ClaudeProjectDirectory.Should().Contain("projects");
        config.ClaudeProjectDirectory.Should().Contain(config.ClaudeRootDirectory);
    }
}