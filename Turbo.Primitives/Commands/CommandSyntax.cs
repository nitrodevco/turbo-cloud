namespace Turbo.Primitives.Commands;

/// <summary>One executable branch, including its own argument binder and optional extra permission.</summary>
public sealed record CommandSyntax(string Path, ICommandBinder Binder, string? Permission);
