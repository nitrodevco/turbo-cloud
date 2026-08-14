using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.Room.Action;

public record AssignRightsMessage : IMessageEvent
{
    public PlayerId PlayerId { get; init; }
}
