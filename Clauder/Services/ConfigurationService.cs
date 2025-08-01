using System.Text.Json;
using Clauder.Models;

namespace Clauder.Services;

using Abstractions;

public class ConfigurationService : IConfigurationService
{
    private const string ConfigFileName = "clauder-config.json";
    private readonly static Lazy<string> ConfigFilePath = new(GetConfigFilePath);
    private readonly static JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };
    private ClaudeConfiguration? _cachedConfiguration;

    public static string GetConfigFilePath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDir = Path.Combine(appDataPath, ".clauder");
            Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, ConfigFileName);
        }
        else if (OperatingSystem.IsMacOS())
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(homeDirectory, "Library", "Application Support", ".clauder");
            Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, ConfigFileName);
        }
        else // Linux and other Unix-like systems
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(homeDirectory, ".clauder");
            Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, ConfigFileName);
        }
    }

    public async Task<ClaudeConfiguration> GetConfigurationAsync()
    {
        if (this._cachedConfiguration != null)
        {
            return this._cachedConfiguration;
        }

        var configPath = ConfigFilePath.Value;

        if (!File.Exists(configPath))
        {
            this._cachedConfiguration = ClaudeConfiguration.CreateDefault();
            await this.SaveConfigurationAsync(this._cachedConfiguration);
            return this._cachedConfiguration;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            this._cachedConfiguration = JsonSerializer.Deserialize<ClaudeConfiguration>(json) ?? ClaudeConfiguration.CreateDefault();
            return this._cachedConfiguration;
        }
        catch
        {
            this._cachedConfiguration = ClaudeConfiguration.CreateDefault();
            return this._cachedConfiguration;
        }
    }

    public async Task SaveConfigurationAsync(ClaudeConfiguration configuration)
    {
        var configPath = ConfigFilePath.Value;

        var json = JsonSerializer.Serialize(configuration, Options);
        await File.WriteAllTextAsync(configPath, json);

        this._cachedConfiguration = configuration;
    }

    public ClaudeConfiguration GetConfiguration()
    {
        return this.GetConfigurationAsync().GetAwaiter().GetResult();
    }

    public void SaveConfiguration(ClaudeConfiguration configuration)
    {
        this.SaveConfigurationAsync(configuration).GetAwaiter().GetResult();
    }

    public void InvalidateCache()
    {
        this._cachedConfiguration = null;
    }

    private static bool IsFirstTimeSetup()
    {
        var configPath = ConfigFilePath.Value;
        return !File.Exists(configPath);
    }

    public async Task<(ClaudeConfiguration configuration, bool isFirstTime)> GetConfigurationWithFirstTimeCheckAsync()
    {
        var isFirstTime = IsFirstTimeSetup();
        var configuration = await this.GetConfigurationAsync();
        return (configuration, isFirstTime);
    }
}