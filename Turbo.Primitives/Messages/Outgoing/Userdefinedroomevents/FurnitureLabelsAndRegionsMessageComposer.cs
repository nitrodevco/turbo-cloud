using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Every label and region of the room, sent on each entry, with no entries when there are none
/// (official client capture, hhus 2026-10-13: <c>00000000</c> right after <c>WiredEnvironment</c>).
/// The client replaces what it had (Flash <c>RoomEngine.replaceFurnitureLabelsAndRegions</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureLabelsAndRegionsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<FurnitureLabelSnapshot> Entries { get; init; }
}
