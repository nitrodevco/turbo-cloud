using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Incoming.Register;

public record UpdateFigureDataMessage : IMessageEvent
{
    public required string Figure { get; init; }
    public required AvatarGenderType Gender { get; init; }
}
