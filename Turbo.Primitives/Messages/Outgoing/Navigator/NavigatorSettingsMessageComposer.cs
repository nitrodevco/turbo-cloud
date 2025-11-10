using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record NavigatorSettingsMessageComposer : IComposer
{
    public int HomeRoomId { get; init; }
    public int RoomIdToEnter { get; init; }
}
