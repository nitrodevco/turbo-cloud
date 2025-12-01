using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record ToggleStaffPickMessage : IMessageEvent
{
    public int RoomId { get; init; }
    public bool IsStaffPicked { get; init; }
}
