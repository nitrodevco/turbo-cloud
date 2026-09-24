using System.Collections.Immutable;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record UpdateGuildBadgeMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }

    /// <summary>One base and up to four symbols, in layer order.</summary>
    public ImmutableArray<GuildBadgePartSnapshot> BadgeParts { get; init; } =
        ImmutableArray<GuildBadgePartSnapshot>.Empty;
}
