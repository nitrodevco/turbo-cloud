using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record GetGuildMembersMessage : IMessageEvent
{
    public GuildId GuildId { get; init; }
    public int PageIndex { get; init; }
    public string NameFilter { get; init; } = string.Empty;
    public GuildMemberSearchType SearchType { get; init; }
}
