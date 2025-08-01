using System.Text.Json.Serialization;

namespace Clauder.Models;

public class ClaudeConfiguration
{
    [JsonPropertyName("claudeExecutablePath")]
    public string ClaudeExecutablePath { get; set; } = GetDefaultClaudeExecutablePath();

    [JsonPropertyName("claudeDataDirectory")]
    public string ClaudeProjectDirectory { get; set; } = GetDefaultClaudeProjectsPath();

    [JsonPropertyName("claudeRootDirectory")]
    public string ClaudeRootDirectory { get; set; } = GetDefaultClaudeRootPath();

    private static string GetDefaultClaudeExecutablePath()
    {
        // Try to find claude in common locations
        var possiblePaths = new[]
        {
            GetDefaultClaudeBinaryPath(),
            "claude", // Assume it's in PATH
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Default fallback
        return "claude";
    }

    private static string GetDefaultClaudeBinaryPath()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            // Check common Windows locations for claude
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd"),
                Path.Combine(homeDirectory, "AppData", "Roaming", "npm", "claude.cmd"),
                "claude.cmd",
                "claude"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return "claude";
        }
        else
        {
            // Unix-like systems (Linux, macOS)
            return Path.Combine(homeDirectory, ".npm-global", "bin", "claude");
        }
    }

    private static string GetDefaultClaudeProjectsPath()
    {
        return Path.Combine(GetDefaultClaudeRootPath(), "projects");
    }

    private static string GetDefaultClaudeRootPath()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, ".claude");
    }

    public static ClaudeConfiguration CreateDefault()
    {
        return new ClaudeConfiguration();
    }
}