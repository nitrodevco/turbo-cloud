using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record GetHabboGroupDetailsMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }

    /// <summary>
    /// Whether the client wants its details window opened, or is only refreshing one it already
    /// has. It is echoed back untouched; the server does not act on it.
    /// </summary>
    public bool OpenDetails { get; init; }
}
