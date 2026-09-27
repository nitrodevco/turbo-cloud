using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Notifications;

internal class UnseenItemsEventMessageComposerSerializer(int header)
    : AbstractSerializer<UnseenItemsEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UnseenItemsEventMessageComposer message)
    {
        packet.WriteInteger(message.Items.Count);

        foreach (var (category, ids) in message.Items)
        {
            packet.WriteInteger((int)category).WriteInteger(ids.Length);

            foreach (var id in ids)
                packet.WriteInteger(id);
        }
    }
}
