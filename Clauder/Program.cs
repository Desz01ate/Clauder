using Clauder.Abstractions;
using Clauder.Pages;
using Clauder.Services;
using Conspectre.Abstractions;
using Conspectre.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

// Setup dependency injection container
var services =
    new ServiceCollection()
        .AddConspectreHost<ProjectsPage>()
        .ConfigureServices()
        .BuildServiceProvider();

// Initialize configuration service first to ensure default config is created
var configService = (ConfigurationService)services.GetRequiredService<IConfigurationService>();
var (configuration, isFirstTime) = await configService.GetConfigurationWithFirstTimeCheckAsync();

// Show first-time setup message
if (isFirstTime)
{
    AnsiConsole.MarkupLine("[green]Welcome to Clauder![/]");
    AnsiConsole.MarkupLine("[dim]Default configuration has been created.[/]");
    AnsiConsole.MarkupLine($"[dim]Config file location: {ConfigurationService.GetConfigFilePath()}[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[yellow]Claude executable path: {configuration.ClaudeExecutablePath}[/]");
    AnsiConsole.MarkupLine($"[yellow]Claude data directory: {configuration.ClaudeProjectDirectory}[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]You can change these settings later in the Settings menu.[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
    Console.ReadKey();
}

// Check if Claude directory exists using the configured path
if (!ClaudeDataService.ClaudeDirectoryExists(configuration))
{
    AnsiConsole.MarkupLine($"[red]Claude projects directory not found at: {configuration.ClaudeProjectDirectory}[/]");
    AnsiConsole.MarkupLine("[yellow]You can configure the Claude data directory in Settings (press 'O' from the main screen).[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

// Create and run application host with DI
var applicationHost = services.GetRequiredService<IApplicationHost>();

await applicationHost.RunAsync();