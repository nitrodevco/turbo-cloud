using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record UpdateGuildColorsMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }
    public int PrimaryColorId { get; init; }
    public int SecondaryColorId { get; init; }
}
