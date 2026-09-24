using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record ApproveMembershipRequestMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }
    public PlayerId PlayerId { get; init; }
}
