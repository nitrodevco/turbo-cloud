using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

public record PassCarryItemMessage : IMessageEvent
{
    public required PlayerId TargetId { get; init; }
}
