using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record DeselectFavouriteHabboGroupMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }
}
