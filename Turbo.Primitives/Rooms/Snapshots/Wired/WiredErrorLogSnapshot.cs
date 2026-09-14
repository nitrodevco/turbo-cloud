using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// One row of the wired monitor's error list: errors are aggregated by name and category, so a
/// repeating fault shows once with a throw count and the time since it last happened.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredErrorLogSnapshot
{
    [Id(0)]
    public required int ErrorId { get; init; }

    [Id(1)]
    public required string ErrorName { get; init; }

    [Id(2)]
    public required string Category { get; init; }

    [Id(3)]
    public required int ThrowCount { get; init; }

    [Id(4)]
    public required long MsSinceLastOccurrence { get; init; }
}
