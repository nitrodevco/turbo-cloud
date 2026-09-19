using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Bots;

public record CommandBotMessage : IMessageEvent
{
    public required int BotId { get; init; }
    public required BotSkillType Skill { get; init; }
    public required string Data { get; init; }
}
