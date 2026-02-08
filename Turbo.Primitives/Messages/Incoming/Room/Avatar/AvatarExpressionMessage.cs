using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

public record AvatarExpressionMessage : IMessageEvent
{
    public required int ExpressionId { get; init; }
}
