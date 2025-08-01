namespace Clauder.Abstractions;

public interface IDisplay : IDisposable
{
    string Title { get; }

    Task DisplayAsync();
    
    Task PushBackAsync();
}