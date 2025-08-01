namespace Clauder.Abstractions;

using Models;

public interface IConfigurationService
{
    ClaudeConfiguration GetConfiguration();

    Task<ClaudeConfiguration> GetConfigurationAsync();

    void SaveConfiguration(ClaudeConfiguration configuration);
}