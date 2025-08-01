using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Clauder.Services;

using Pages;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ClaudeConfiguration>(
            static sp => sp.GetRequiredService<IConfigurationService>().GetConfiguration());
        services.AddSingleton<ClaudeDataService>();
        services.AddSingleton<IClaudeProcessService, ClaudeProcessService>();
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
        services.AddSingleton<RenderingHost>();

        // Register pages
        services.AddTransient<ProjectsPage>();
        services.AddTransient<SessionsPage>();
        services.AddTransient<SettingsPage>();

        return services;
    }
}