namespace Clauder.Services;

using Clauder.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;

public sealed class ConsoleRenderEngine : IRenderEngine
{
    public async Task RunRenderLoopAsync(IRenderable initialLayout, Func<Task<IRenderable>> layoutProvider, Func<ConsoleKeyInfo, Task> inputHandler, Action<Action> registerRefreshCallback, CancellationToken cancellationToken = default)
    {
        await AnsiConsole.Live(initialLayout)
                         .StartAsync(async ctx =>
                         {
                             ctx.Refresh();
                             
                             // Register the refresh callback so external components can trigger layout updates
                             registerRefreshCallback(async () =>
                             {
                                 try
                                 {
                                     var newLayout = await layoutProvider();
                                     ctx.UpdateTarget(newLayout);
                                     ctx.Refresh();
                                 }
                                 catch (Exception ex)
                                 {
                                     System.Diagnostics.Debug.WriteLine($"Refresh callback error: {ex.Message}");
                                 }
                             });
                             
                             while (!cancellationToken.IsCancellationRequested)
                             {
                                 var keyInfo = Console.ReadKey(true);
                                 
                                 try
                                 {
                                     await inputHandler(keyInfo);
                                     
                                     // Update layout after input
                                     var newLayout = await layoutProvider();
                                     ctx.UpdateTarget(newLayout);
                                     ctx.Refresh();
                                 }
                                 catch (OperationCanceledException)
                                 {
                                     // Expected when cancellation is requested
                                     break;
                                 }
                                 catch (Exception ex)
                                 {
                                     // Log but continue
                                     System.Diagnostics.Debug.WriteLine($"Input handling error: {ex.Message}");
                                 }
                             }
                         });
    }

    public void Dispose()
    {
        // Nothing to dispose for console rendering
    }
}