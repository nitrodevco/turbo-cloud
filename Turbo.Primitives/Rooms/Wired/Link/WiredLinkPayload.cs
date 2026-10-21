using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>
/// What a send box puts on a network: a message (Send Link Message) or a dataframe of named
/// values (Send Link Dataframe), and the room it was sent from.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkPayload
{
    [Id(0)]
    public required RoomId SourceRoomId { get; init; }

    /// <summary>The message; null for a dataframe.</summary>
    [Id(1)]
    public string? Message { get; init; }

    /// <summary>The dataframe's rows, key then value, in the sender's order; empty for a message.</summary>
    [Id(2)]
    public ImmutableArray<WiredLinkDataframeRow> Rows { get; init; } = [];

    public bool IsDataframe => Message is null;
}

/// <summary>One named value of a dataframe.</summary>
[GenerateSerializer, Immutable]
public readonly record struct WiredLinkDataframeRow(
    [property: Id(0)] string Key,
    [property: Id(1)] string Value
);
