using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record MoveAvatarMessage : IMessageEvent
{
    public required int TargetX { get; init; }

    public required int TargetY { get; init; }
}
