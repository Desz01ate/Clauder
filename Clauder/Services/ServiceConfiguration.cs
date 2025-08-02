using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Clauder.Services;

using System.Reflection;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ClaudeConfiguration>(
            static sp => sp.GetRequiredService<IConfigurationService>().GetConfiguration());
        services.AddSingleton<ClaudeDataService>();
        services.AddSingleton<IPageFactory, PageFactory>();

        // Configure channel for navigation
        services.AddSingleton<Channel<NavigationCommand>>(
            static _ => Channel.CreateBounded<NavigationCommand>(1));

        services.AddSingleton<ChannelWriter<NavigationCommand>>(sp =>
            sp.GetRequiredService<Channel<NavigationCommand>>().Writer);

        services.AddSingleton<ChannelReader<NavigationCommand>>(sp =>
            sp.GetRequiredService<Channel<NavigationCommand>>().Reader);

        services.AddSingleton<INavigationContext, NavigationContext>();

        // Configure channel for toast notifications
        services.AddSingleton<Channel<ToastCommand>>(
            static _ => Channel.CreateBounded<ToastCommand>(10));

        services.AddSingleton<ChannelWriter<ToastCommand>>(sp =>
            sp.GetRequiredService<Channel<ToastCommand>>().Writer);

        services.AddSingleton<ChannelReader<ToastCommand>>(sp =>
            sp.GetRequiredService<Channel<ToastCommand>>().Reader);

        services.AddSingleton<IToastContext, ToastContext>();

        // Register new architecture components
        services.AddSingleton<IRenderEngine, ConsoleRenderEngine>();
        services.AddSingleton<IPageManager, PageManager>();
        services.AddSingleton<IToastManager, ToastManager>();
        services.AddSingleton<IInputProcessor, InputProcessor>();
        services.AddSingleton<ILayoutManager, LayoutManager>();
        services.AddSingleton<IApplicationHost, ApplicationHost>();

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