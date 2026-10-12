using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

/// <summary>
/// Flash <c>RecolorUpdateData</c> per entry: the furni ids, then each channel as a short.
/// </summary>
internal class FurniRecolorUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<FurniRecolorUpdateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurniRecolorUpdateMessageComposer message
    )
    {
        packet.WriteInteger(message.Updates.Length);

        foreach (var update in message.Updates)
        {
            packet.WriteInteger(update.ObjectIds.Length);

            foreach (var objectId in update.ObjectIds)
                packet.WriteInteger((int)objectId);

            packet
                .WriteShort((short)update.Red)
                .WriteShort((short)update.Green)
                .WriteShort((short)update.Blue);
        }
    }
}
