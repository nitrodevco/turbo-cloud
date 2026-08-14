using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

public record ChangePostureMessage : IMessageEvent
{
    public AvatarPostureType PostureType { get; init; }
}
