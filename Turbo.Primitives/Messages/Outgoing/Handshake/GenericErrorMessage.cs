using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record GenericErrorMessage : IComposer
{
    [Id(0)]
    public required RoomGenericErrorType ErrorCode { get; init; }
}
