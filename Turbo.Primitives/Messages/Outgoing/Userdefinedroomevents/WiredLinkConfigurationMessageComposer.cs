using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>A WIRED Link's configuration and its owner's networks, which the Link's window shows.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkConfigurationMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required int FurniId { get; init; }

    [Id(2)]
    public required string SelectedNetworkId { get; init; }

    [Id(3)]
    public required bool ReceiveLocal { get; init; }

    [Id(4)]
    public required ImmutableArray<WiredLinkNetworkSnapshot> Networks { get; init; }
}
