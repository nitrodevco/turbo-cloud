using Turbo.Primitives.Messages.Outgoing.Room.Bots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Bots;

internal class BotCommandConfigurationMessageComposerSerializer(int header)
    : AbstractSerializer<BotCommandConfigurationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BotCommandConfigurationMessageComposer message
    )
    {
        //
    }
}
