namespace Clauder.Pages;

using Clauder.Abstractions;
using Spectre.Console;

public sealed class SettingsPage : IDisplay
{
    private readonly INavigationService _navigationService;

    public SettingsPage(INavigationService navigationService)
    {
        this._navigationService = navigationService;
    }

    public string Title => "[#CC785C]Settings[/]";

    public async Task DisplayAsync(CancellationToken cancellationToken = default)
    {
        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        var rule = new Rule("[#CC785C]Settings[/]")
        {
            Justification = Justify.Left,
        };

        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]Settings page - coming soon![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim][red]B[/] Back[/]");

        var key = Console.ReadKey(true).Key;

        if (key == ConsoleKey.B || key == ConsoleKey.Escape)
        {
            await this.PushBackAsync();
        }
    }

    public async Task PushBackAsync()
    {
        await this._navigationService.NavigateBackAsync();
    }

    public void Dispose()
    {
    }
}