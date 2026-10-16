using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One of Habbo's reception values beside the hotel's: a variable (its value as the hotel keeps
/// it, JSON) or a text (as the file writes it).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HotelViewHabboEntry
{
    [Id(0)]
    public required string Key { get; init; }

    /// <summary>Habbo's value.</summary>
    [Id(1)]
    public required string Value { get; init; }

    /// <summary>The hotel's value; null when it has none.</summary>
    [Id(2)]
    public string? Current { get; init; }
}
