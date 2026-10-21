using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>The Links connected to one network, for the window's "Connected rooms" list.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkConnectionsMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required int FurniId { get; init; }

    [Id(2)]
    public required string NetworkId { get; init; }

    [Id(3)]
    public required ImmutableArray<WiredLinkConnectionSnapshot> Connections { get; init; }
}
