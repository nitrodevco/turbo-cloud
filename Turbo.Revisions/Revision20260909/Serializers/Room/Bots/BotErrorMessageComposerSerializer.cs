using Turbo.Primitives.Messages.Outgoing.Room.Bots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Bots;

internal class BotErrorMessageComposerSerializer(int header)
    : AbstractSerializer<BotErrorMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, BotErrorMessageComposer message)
    {
        packet.WriteInteger((int)message.Error);
    }
}
