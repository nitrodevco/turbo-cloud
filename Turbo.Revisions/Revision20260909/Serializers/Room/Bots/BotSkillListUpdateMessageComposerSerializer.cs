using Turbo.Primitives.Messages.Outgoing.Room.Bots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Bots;

internal class BotSkillListUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<BotSkillListUpdateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BotSkillListUpdateMessageComposer message
    )
    {
        packet.WriteInteger(message.BotId).WriteInteger(message.Skills.Length);

        foreach (var skill in message.Skills)
            packet.WriteInteger((int)skill.Skill).WriteString(skill.Data);
    }
}
