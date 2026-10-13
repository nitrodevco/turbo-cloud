using Turbo.Primitives.Habbicons.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Habbicons;

/// <summary>One Habbicon as the client's <c>_-Ne</c> reads it.</summary>
internal static class HabbiconSerializer
{
    public static void Serialize(IServerPacket packet, HabbiconSnapshot habbicon) =>
        packet
            .WriteInteger(habbicon.HabbiconId)
            .WriteString(habbicon.Name)
            .WriteInteger(habbicon.CollectionId)
            .WriteInteger((int)habbicon.State)
            .WriteInteger(habbicon.PriceCredits)
            .WriteInteger(habbicon.PriceActivityPoints)
            .WriteInteger(habbicon.ActivityPointType);
}
