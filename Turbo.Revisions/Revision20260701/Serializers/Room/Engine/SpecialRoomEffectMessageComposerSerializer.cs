using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Engine;

internal class SpecialRoomEffectMessageComposerSerializer(int header)
    : AbstractSerializer<SpecialRoomEffectMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SpecialRoomEffectMessageComposer message
    )
    {
        packet.WriteInteger(message.EffectId);
    }
}
