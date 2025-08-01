namespace Clauder.Pages;

using Abstractions;
using Spectre.Console;

public sealed class MainPage : IDisplay
{
    private readonly Stack<IDisplay> displayStack = new();

    public MainPage()
    {
    }

    public string Title => this.displayStack.TryPeek(out var currentDisplay) ? currentDisplay.Title : "[#CC785C]Clauder[/]";

    public Task DisplayAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var display in this.displayStack)
        {
            display.Dispose();
        }
    }
}