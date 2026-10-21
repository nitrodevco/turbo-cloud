using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// A network was deleted while a Link connected to it is in the owner's room: the Link's window,
/// if open, drops the network and shows it disconnected (AS3 <c>WiredLinkController.onInvalidated</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkConfigurationInvalidatedMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required int FurniId { get; init; }

    [Id(2)]
    public required string DeletedNetworkId { get; init; }
}
