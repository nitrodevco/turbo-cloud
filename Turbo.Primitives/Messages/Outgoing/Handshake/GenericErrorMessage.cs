using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record GenericErrorMessage : IComposer
{
    public required RoomGenericErrorType ErrorCode { get; init; }
}
