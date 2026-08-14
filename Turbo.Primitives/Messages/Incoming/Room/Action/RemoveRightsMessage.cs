using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.Room.Action;

public record RemoveRightsMessage : IMessageEvent
{
    public List<PlayerId> PlayerIds { get; init; } = [];
}
