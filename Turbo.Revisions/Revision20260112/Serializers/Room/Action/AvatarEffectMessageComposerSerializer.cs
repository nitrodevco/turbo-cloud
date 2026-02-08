using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Action;

internal class AvatarEffectMessageComposerSerializer(int header)
    : AbstractSerializer<AvatarEffectMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, AvatarEffectMessageComposer message)
    {
        packet
            .WriteInteger(message.UserId)
            .WriteInteger(message.EffectId)
            .WriteInteger(message.DelayMilliseconds);
    }
}
