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
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content"),
                new Layout("Footer").Size(2)
            );

        layout["Header"].Update(new Rule("[#CC785C]Settings[/]") { Justification = Justify.Left });
        layout["Content"].Update(new Markup("[yellow]Settings page - coming soon![/]"));
        layout["Footer"].Update(new Markup("[dim][red]B[/] Back[/]"));

        await AnsiConsole.Live(layout)
            .StartAsync(async ctx =>
            {
                var key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.B || key == ConsoleKey.Escape)
                {
                    await this.PushBackAsync();
                }
            });
    }

    public async Task PushBackAsync()
    {
        await this._navigationService.NavigateBackAsync();
    }

    public void Dispose()
    {
    }
}