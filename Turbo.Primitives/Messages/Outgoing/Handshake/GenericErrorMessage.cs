using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record GenericErrorMessage : IComposer
{
    public required RoomGenericErrorType ErrorCode { get; init; }
}
