namespace Clauder.Services;

using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;
using Commands;

public sealed class NavigationContext : INavigationContext
{
    private readonly ChannelWriter<NavigationCommand> _navigationWriter;

    public NavigationContext(ChannelWriter<NavigationCommand> navigationWriter)
    {
        this._navigationWriter = navigationWriter;
    }

    public async Task NavigateToAsync<T>(params object[] args)
        where T : class, IPage
    {
        var command = new NavigateToCommand(typeof(T), args);

        await this._navigationWriter.WriteAsync(command);
    }

    public async Task NavigateBackAsync()
    {
        var command = new NavigateBackCommand();

        await this._navigationWriter.WriteAsync(command);
    }

    public void Dispose()
    {
        this._navigationWriter.Complete();
    }
}