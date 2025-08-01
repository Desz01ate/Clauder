namespace Clauder.Pages;

using System.Diagnostics;
using Clauder.Abstractions;
using Clauder.Services;
using Models;
using Spectre.Console;
using Spectre.Console.Rendering;

public sealed record SettingsMenuItem(string Title, Action Action);

public sealed class SettingsPage : IPage, IInputHandler
{
    private readonly IReadOnlyList<SettingsMenuItem> _menuItems;
    private readonly IConfigurationService _configurationService;
    private int _selectedIndex;

    public SettingsPage(IConfigurationService configurationService)
    {
        this._configurationService = configurationService;
        this._menuItems = new List<SettingsMenuItem>
        {
            new("Open Claude directory", this.OpenClaudeDirectory),
            new("Configure Claude executable path", () => this.ConfigureConfigurationField(
                "Claude executable path",
                config => config.ClaudeExecutablePath,
                (config, value) => config.ClaudeExecutablePath = value)),
            new("Configure Claude data directory", () => this.ConfigureConfigurationField(
                "Claude data directory",
                config => config.ClaudeProjectDirectory,
                (config, value) => config.ClaudeProjectDirectory = value)),
        };
    }

    public string Title => "[#CC785C]Settings[/]";

    public ValueTask<IRenderable> RenderHeaderAsync()
    {
        var header = new Rule("[#CC785C]Settings[/]") { Justification = Justify.Left };

        return ValueTask.FromResult<IRenderable>(header);
    }

    public ValueTask<IRenderable> RenderBodyAsync()
    {
        var body = this.CreateSettingsMenu();

        return ValueTask.FromResult<IRenderable>(body);
    }

    public ValueTask<IRenderable> RenderFooterAsync()
    {
        var footer = new Markup("[dim][green]↑↓[/] Navigate • [green]Enter[/] Select • [red]B[/] Back[/]");

        return ValueTask.FromResult<IRenderable>(footer);
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        // No async initialization needed for settings page
        return ValueTask.CompletedTask;
    }

    public Task<bool> HandleInputAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                this._selectedIndex = this._selectedIndex > 0 ? this._selectedIndex - 1 : this._menuItems.Count - 1;
                return Task.FromResult(true);

            case ConsoleKey.DownArrow:
                this._selectedIndex = this._selectedIndex < this._menuItems.Count - 1 ? this._selectedIndex + 1 : 0;
                return Task.FromResult(true);

            case ConsoleKey.Enter:
                this._menuItems[this._selectedIndex].Action();
                return Task.FromResult(true);

            default:
                return Task.FromResult(false); // Let global handler handle B/Escape
        }
    }


    private Panel CreateSettingsMenu()
    {
        var menuItems = new List<string>();

        for (var i = 0; i < this._menuItems.Count; i++)
        {
            var isSelected = i == this._selectedIndex;
            var selectionMarker = isSelected ? "[yellow]>[/] " : "  ";
            var itemText = isSelected ? $"[yellow]{this._menuItems[i].Title}[/]" : $"[dim]{this._menuItems[i].Title}[/]";
            menuItems.Add($"{selectionMarker}{itemText}");
        }

        var menuContent = string.Join("\n", menuItems);

        return new Panel(new Markup(menuContent))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1),
        }.Expand();
    }

    private void OpenClaudeDirectory()
    {
        var configuration = this._configurationService.GetConfiguration();
        var claudeDirectory = configuration.ClaudeRootDirectory;

        if (!Directory.Exists(claudeDirectory))
        {
            return;
        }

        try
        {
            ProcessStartInfo processStartInfo;

            if (OperatingSystem.IsWindows())
            {
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = claudeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = claudeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }
            else // Linux and other Unix-like systems
            {
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = claudeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            Process.Start(processStartInfo);
        }
        catch
        {
            // Silently handle any errors opening the directory
        }
    }

    private void ConfigureConfigurationField(
        string fieldDisplayName,
        Func<ClaudeConfiguration, string> getter,
        Action<ClaudeConfiguration, string> setter)
    {
        var configuration = this._configurationService.GetConfiguration();

        Console.WriteLine();
        Console.Write($"Current {fieldDisplayName}: {getter(configuration)}");
        Console.WriteLine();
        Console.Write($"Enter new {fieldDisplayName} (or press Enter to keep current): ");

        var newValue = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(newValue))
        {
            var processedValue = newValue.Trim();

            processedValue = Environment.ExpandEnvironmentVariables(processedValue);

            // Replace ~ with home directory on Unix-like systems
            if (processedValue.StartsWith('~'))
            {
                var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                processedValue = processedValue.Replace("~", homeDirectory);
            }

            setter(configuration, processedValue);
            this._configurationService.SaveConfiguration(configuration);
            Console.WriteLine($"{fieldDisplayName} updated successfully!");
            Console.WriteLine("Note: You may need to restart the application for changes to take full effect.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
        else
        {
            Console.WriteLine("No changes made.");
        }
    }

    public void Dispose()
    {
    }
}