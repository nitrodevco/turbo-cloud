using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record ToggleStaffPickMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
    public bool IsStaffPicked { get; init; }
}
