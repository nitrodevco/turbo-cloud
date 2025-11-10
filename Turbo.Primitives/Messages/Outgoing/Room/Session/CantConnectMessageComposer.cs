using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public sealed record CantConnectMessageComposer : IComposer
{
    public required RoomConnectionErrorType ErrorType { get; init; }
    public string? AdditionalInfo { get; init; }
}
