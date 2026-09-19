using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Messages.Incoming.Room.Bots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Bots;

internal class CommandBotMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CommandBotMessage
        {
            BotId = packet.PopInt(),
            Skill = (BotSkillType)packet.PopInt(),
            Data = packet.PopString(),
        };
}
