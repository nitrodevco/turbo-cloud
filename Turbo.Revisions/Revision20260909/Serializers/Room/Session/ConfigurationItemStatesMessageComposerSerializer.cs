using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Session;

internal class ConfigurationItemStatesMessageComposerSerializer(int header)
    : AbstractSerializer<ConfigurationItemStatesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ConfigurationItemStatesMessageComposer message
    )
    {
        packet
            .WriteBoolean(message.IsHanditemControlBlocked)
            .WriteBoolean(message.ChooserDisabled)
            .WriteBoolean(message.FreeFurniMovementsEnabled)
            .WriteBoolean(message.InvisibleFurni);
    }
}
