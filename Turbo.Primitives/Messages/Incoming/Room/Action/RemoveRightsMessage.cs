using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Action;

public record RemoveRightsMessage : IMessageEvent
{
    public List<int> UserIds { get; init; } = [];
}
