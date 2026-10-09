using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Camera;

internal class CameraPublishStatusMessageComposerSerializer(int header)
    : AbstractSerializer<CameraPublishStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CameraPublishStatusMessageComposer message
    )
    {
        packet.WriteBoolean(message.IsOk);
        packet.WriteInteger(message.SecondsToWait);

        if (message.IsOk && message.ExtraDataId is not null)
            packet.WriteString(message.ExtraDataId);
    }
}
