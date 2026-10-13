using Turbo.Primitives.Messages.Outgoing.Habbicons;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Habbicons;

/// <summary>The collections as the client's <c>_-01f</c> / <c>_-11f</c> read them.</summary>
internal class HabbiconShopDataMessageComposerSerializer(int header)
    : AbstractSerializer<HabbiconShopDataMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabbiconShopDataMessageComposer message)
    {
        packet.WriteInteger(message.Collections.Length);

        foreach (var collection in message.Collections)
        {
            packet
                .WriteInteger(collection.CollectionId)
                .WriteString(collection.Name)
                .WriteBoolean(collection.Completed)
                .WriteInteger(collection.RewardHabbiconId)
                .WriteInteger((int)collection.RewardState)
                .WriteInteger(collection.PriceCredits)
                .WriteInteger(collection.PriceActivityPoints)
                .WriteInteger(collection.ActivityPointType)
                .WriteInteger(collection.Habbicons.Length);

            foreach (var habbicon in collection.Habbicons)
                HabbiconSerializer.Serialize(packet, habbicon);
        }
    }
}
