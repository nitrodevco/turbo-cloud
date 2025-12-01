using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record GuildBaseSearchMessage : IMessageEvent
{
    public int Unknown { get; init; }
}
