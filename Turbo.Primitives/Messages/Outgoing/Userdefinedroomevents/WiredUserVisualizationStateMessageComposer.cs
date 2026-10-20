using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// The room's WIRED Set Visibility and Set Highlight states on users, pets and bots (by room
/// index) that "everyone, including users who enter later" sees (Flash
/// <c>WiredUserVisualizationStateMessageParser</c>, WIN63-202610201032), for a player walking in;
/// the client applies them without easing (<c>RoomEngine.initializeWiredUserVisualizationStates</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredUserVisualizationStateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<WiredTransparencyStateEntry> Transparencies { get; init; }

    [Id(1)]
    public required ImmutableArray<WiredHighlightStateEntry> Highlights { get; init; }
}
