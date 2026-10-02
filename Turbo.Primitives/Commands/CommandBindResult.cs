using System;

namespace Turbo.Primitives.Commands;

/// <summary>
/// The outcome of binding a line to a command's arguments: the arguments, or the shared reply key
/// (<see cref="CommandReplyKeys"/>) that says why not, with the values its text names.
/// </summary>
public readonly record struct CommandBindResult(
    object? Arguments,
    string? ErrorKey,
    string[] Parameters
)
{
    public int ErrorStart { get; init; } = -1;
    public int ErrorEnd { get; init; } = -1;
    public string? RequiredPermission { get; init; }
    public bool Succeeded => ErrorKey is null;

    public static CommandBindResult Success(object arguments) => new(arguments, null, []);

    public static CommandBindResult Failure(string errorKey, params string[] parameters) =>
        new(null, errorKey, parameters);
}
