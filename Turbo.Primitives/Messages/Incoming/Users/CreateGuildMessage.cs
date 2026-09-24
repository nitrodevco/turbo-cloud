using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record CreateGuildMessage : IMessageEvent
{
    public required GuildCreationRequestSnapshot Request { get; init; }
}
