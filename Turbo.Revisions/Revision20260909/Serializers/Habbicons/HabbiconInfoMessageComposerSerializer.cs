using Turbo.Primitives.Messages.Outgoing.Habbicons;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Habbicons;

internal class HabbiconInfoMessageComposerSerializer(int header)
    : AbstractSerializer<HabbiconInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabbiconInfoMessageComposer message) =>
        HabbiconSerializer.Serialize(packet, message.Habbicon);
}
