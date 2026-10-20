using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// A WIRED Set Highlight on users, pets and bots (Flash <c>WiredUserHighlightMessageParser</c>,
/// WIN63-202610201032): their room indices, then the highlight.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredUserHighlightMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<int> RoomIndices { get; init; }

    [Id(1)]
    public required WiredHighlightSnapshot Highlight { get; init; }
}
