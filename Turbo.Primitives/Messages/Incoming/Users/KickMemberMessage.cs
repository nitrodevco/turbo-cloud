using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.Users;

/// <summary>
/// Removing a member, which is also how somebody leaves: the client sends this with itself as
/// the target.
/// </summary>
public record KickMemberMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }
    public PlayerId PlayerId { get; init; }

    /// <summary>Whether to block them from rejoining rather than only removing them.</summary>
    public bool Block { get; init; }
}
