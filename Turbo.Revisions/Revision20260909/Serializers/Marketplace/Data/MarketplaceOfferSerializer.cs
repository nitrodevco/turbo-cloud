using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace.Data;

/// <summary>
/// One offer as Flash's marketplace parsers read it: id, status, furni type, then by type the
/// sprite id with stuff data (floor), with the data string (wall) or with only the serial and
/// series size (limited edition), then price, minutes left and average price. Checked against the
/// official client capture of 2026-10-13 (two full lists, 10240 and 10440 bytes, read to the end).
/// </summary>
internal static class MarketplaceOfferSerializer
{
    public static void SerializeHead(IServerPacket packet, MarketplaceOfferSnapshot offer)
    {
        packet
            .WriteInteger(offer.OfferId)
            .WriteInteger((int)offer.Status)
            .WriteInteger((int)offer.FurniType);

        switch (offer.FurniType)
        {
            case MarketplaceFurniType.Wall:
                packet.WriteInteger(offer.FurniTypeId).WriteString(offer.ExtraData);
                break;
            case MarketplaceFurniType.Unique:
                packet
                    .WriteInteger(offer.FurniTypeId)
                    .WriteInteger(offer.StuffData?.UniqueNumber ?? 0)
                    .WriteInteger(offer.StuffData?.UniqueSeries ?? 0);
                break;
            default:
                packet.WriteInteger(offer.FurniTypeId);
                StuffDataSnapshotSerializer.Serialize(
                    packet,
                    offer.StuffData
                        ?? new LegacyStuffSnapshot { StuffBitmask = 0, Data = string.Empty }
                );
                break;
        }

        packet
            .WriteInteger(offer.Price)
            .WriteInteger(offer.TimeLeftMinutes)
            .WriteInteger(offer.AveragePrice);
    }
}
