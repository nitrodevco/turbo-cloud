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
            .WriteBoolean(message.InvisibleFurni)
            // Habbo sends a fifth byte the client does not read yet
            // (_-B4.as:50-65, WIN63-202610131318), zero in every official sample.
            .WriteBoolean(false);
    }
}
