using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// One row of the wired monitor's error list: one of Habbo's fixed errors, with how often it
/// happened and the time since it last did (-1 when it never did).
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
