using Clauder.Pages;
using Clauder.Services;
using Spectre.Console;

if (!ClaudeDataService.ClaudeDirectoryExists())
{
    AnsiConsole.MarkupLine($"[red]Claude projects directory not found at: {ClaudeDataService.GetClaudeProjectsPath()}[/]");

    return;
}

using var main = new MainPage();

await main.DisplayAsync();