using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of the client's external variables.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableEntrySnapshot
{
    [Id(0)]
    public required string Key { get; init; }

    /// <summary>
    /// The value as JSON: <c>"text"</c>, <c>true</c>, <c>120</c>, <c>[1, 2]</c>. For one that follows a
    /// setting or a file, what it follows now.
    /// </summary>
    [Id(1)]
    public required string Value { get; init; }

    /// <summary>The server setting it follows (<c>Turbo:Web:HotelName</c>); null for one with a value of its own.</summary>
    [Id(2)]
    public string? Setting { get; init; }

    /// <summary>
    /// The gamedata file whose address it follows by hash (<see cref="GamedataFiles"/>); null for one
    /// that doesn't.
    /// </summary>
    [Id(3)]
    public string? File { get; init; }
}
