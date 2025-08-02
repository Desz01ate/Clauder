using Clauder.Abstractions;
using Clauder.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Clauder.Services;

using System.Reflection;
using Conspectre.Abstractions;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ClaudeConfiguration>(
            static sp => sp.GetRequiredService<IConfigurationService>().GetConfiguration());
        services.AddSingleton<ClaudeDataService>();

        // Register pages
        var pageTypes =
            Assembly.GetCallingAssembly()
                    .GetExportedTypes()
                    .Where(
                        static t => t.IsAssignableTo(typeof(IPage)) && t is { IsClass: true, IsAbstract: false });

        foreach (var pageType in pageTypes)
        {
            services.AddTransient(pageType);
        }

        return services;
    }
}