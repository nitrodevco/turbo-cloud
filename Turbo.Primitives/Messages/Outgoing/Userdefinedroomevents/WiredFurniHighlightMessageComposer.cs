using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// A WIRED Set Highlight on furni (Flash <c>WiredFurniHighlightMessageParser</c>,
/// WIN63-202610201032): the furni as wired names them to the client (a wall item negative), then
/// the highlight. The client eases to it (<c>RoomEngine.updateWiredFurniHighlight</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredFurniHighlightMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<int> FurniIds { get; init; }

    [Id(1)]
    public required WiredHighlightSnapshot Highlight { get; init; }
}
