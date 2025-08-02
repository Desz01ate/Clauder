namespace Clauder.Commands;

public abstract record NavigationCommand;

public sealed record NavigateToCommand(Type PageType, object[] Args) : NavigationCommand;

public sealed record NavigateBackCommand : NavigationCommand;

public sealed record ExitCommand : NavigationCommand;