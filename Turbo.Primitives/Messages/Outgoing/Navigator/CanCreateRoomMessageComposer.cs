using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record CanCreateRoomMessageComposer : IComposer
{
    public int ResultCode { get; init; }
    public int RoomLimit { get; init; }
}
