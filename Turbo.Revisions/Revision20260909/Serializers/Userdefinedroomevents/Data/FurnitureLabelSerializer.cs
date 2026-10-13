using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>
/// One label or region, as Flash <c>_-MK.parse</c> reads it: int type, int furni, string text
/// (a region's title); a label then has int bubble colour, int visibility, bool always open; a
/// region string description, int x offset, int y offset, int min width, int min height, string
/// colour and its areas as int count, then x, y, width, height each.
/// </summary>
internal static class FurnitureLabelSerializer
{
    public static void Serialize(IServerPacket packet, FurnitureLabelSnapshot entry)
    {
        packet.WriteInteger((int)entry.Type).WriteInteger(entry.ObjectId).WriteString(entry.Text);

        if (entry.Region is not { } region)
        {
            packet
                .WriteInteger(entry.BubbleColor)
                .WriteInteger((int)entry.Visibility)
                .WriteBoolean(entry.AlwaysOpen);

            return;
        }

        packet
            .WriteString(region.Description)
            .WriteInteger(region.XOffset)
            .WriteInteger(region.YOffset)
            .WriteInteger(region.MinWidth)
            .WriteInteger(region.MinHeight)
            .WriteString(region.Color)
            .WriteInteger(region.Areas.Length);

        foreach (var area in region.Areas)
            packet
                .WriteInteger(area.X)
                .WriteInteger(area.Y)
                .WriteInteger(area.Width)
                .WriteInteger(area.Height);
    }
}
