using System;
using Orleans;

namespace Turbo.Primitives.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementFact
{
    [Id(0)]
    public required string OperationId { get; init; }

    [Id(1)]
    public required string Source { get; init; }

    [Id(2)]
    public int Version { get; init; } = 1;

    [Id(3)]
    public required DateTime OccurredAtUtc { get; init; }

    [Id(4)]
    public long Amount { get; init; } = 1;

    [Id(5)]
    public string Value { get; init; } = "";

    [Id(6)]
    public string? SessionId { get; init; }

    [Id(7)]
    public DateTime? IntervalStartUtc { get; init; }

    [Id(8)]
    public DateTime? IntervalEndUtc { get; init; }
}
