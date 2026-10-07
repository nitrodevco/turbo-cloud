using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One row a change set touched. <see cref="Before"/> and <see cref="After"/> are JSON objects of
/// the fields that changed, by furnidata key; <see cref="Before"/> is null for a row the set made.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GamedataChangeSnapshot
{
    [Id(0)]
    public required GamedataRecordType RecordType { get; init; }

    [Id(1)]
    public required int RecordId { get; init; }

    /// <summary>What the row is, for people: a furniture's classname.</summary>
    [Id(2)]
    public required string Label { get; init; }

    [Id(3)]
    public string? Before { get; init; }

    [Id(4)]
    public string? After { get; init; }
}
