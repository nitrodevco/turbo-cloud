using Turbo.Core.Game.Rooms.Constants;
using Turbo.Packets.Abstractions;

namespace Turbo.Packets.Outgoing.Handshake;

public record GenericErrorMessage : IComposer
{
    public RoomGenericErrorType ErrorCode { get; init; }
}
