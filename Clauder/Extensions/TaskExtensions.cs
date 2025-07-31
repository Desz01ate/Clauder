namespace Clauder.Extensions;

using Concur.Implementations;

public static class TaskExtensions
{
    public static async Task WriteTo<T>(this Task<T> task, DefaultChannel<T> channel)
    {
        var result = await task;

        await channel.WriteAsync(result);
    }
    
    public static async Task WriteTo<T>(this ValueTask<T> task, DefaultChannel<T> channel)
    {
        var result = await task;

        await channel.WriteAsync(result);
    }
}