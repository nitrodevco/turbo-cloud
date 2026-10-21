using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// What Habbo's external variables have that the hotel's don't, before anything is written: every
/// key waiting, so staff can pick the ones to take in.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboVariableUpdatePreview
{
    [Id(0)]
    public required HabboVariableVersionSnapshot Version { get; init; }

    /// <summary>Keys Habbo has that the hotel lacks.</summary>
    [Id(1)]
    public required int Added { get; init; }

    /// <summary>Keys the hotel has with another value than Habbo's.</summary>
    [Id(2)]
    public required int Updated { get; init; }

    /// <summary>Keys Habbo had when last taken in and no longer has, which the hotel still has.</summary>
    [Id(3)]
    public required int Removed { get; init; }

    /// <summary>
    /// Keys the hotel changed or removed after taking Habbo's value in, which Habbo has not changed
    /// since: the hotel's own, not offered.
    /// </summary>
    [Id(4)]
    public required int Kept { get; init; }

    /// <summary>
    /// Keys the hotel owns and never takes from Habbo: those that follow a setting or a file, those
    /// the hotel keeps itself, the reception's, and those the ignore list or an address names.
    /// </summary>
    [Id(5)]
    public required int Protected { get; init; }

    [Id(6)]
    public required int Unchanged { get; init; }

    /// <summary>Every key waiting, by key.</summary>
    [Id(7)]
    public required ImmutableArray<HabboVariableUpdateItem> Items { get; init; }
}

/// <summary>One of Habbo's variables waiting to be taken in.</summary>
[GenerateSerializer, Immutable]
public sealed record HabboVariableUpdateItem
{
    [Id(0)]
    public required string Key { get; init; }

    [Id(1)]
    public required HabboVariableUpdateAction Action { get; init; }

    /// <summary>The hotel's value as JSON; null when it has none.</summary>
    [Id(2)]
    public string? Current { get; init; }

    /// <summary>Habbo's value as the hotel would keep it, JSON; null for a key Habbo removed.</summary>
    [Id(3)]
    public string? Incoming { get; init; }

    /// <summary>Habbo's value as its file writes it: bare text; null for a key Habbo removed.</summary>
    [Id(4)]
    public string? Habbo { get; init; }
}
