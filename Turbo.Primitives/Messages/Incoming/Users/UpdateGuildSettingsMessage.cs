using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record UpdateGuildSettingsMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }
    public GuildType GuildType { get; init; }
    public GuildRightsLevel RightsLevel { get; init; }
}
