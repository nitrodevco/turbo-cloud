using System;
using Orleans;

namespace Turbo.Primitives.Settings.Snapshots;

/// <summary>A setting changed in the admin panel: overridden, or put back to what the files say.</summary>
[GenerateSerializer, Immutable]
public sealed record ServerSettingChangeSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Path { get; init; }

    /// <summary>The panel's value before, as JSON; null when the panel didn't set it, or for a secret.</summary>
    [Id(2)]
    public string? Before { get; init; }

    /// <summary>The panel's value after, as JSON; null when it was put back, or for a secret.</summary>
    [Id(3)]
    public string? After { get; init; }

    /// <summary>A secret's change: its values are never kept.</summary>
    [Id(4)]
    public required bool Secret { get; init; }

    [Id(5)]
    public int? PlayerId { get; init; }

    [Id(6)]
    public required DateTime ChangedAt { get; init; }
}
