using System.Collections.Immutable;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>
/// Flash <c>FurniHighlightData</c> / <c>FurniTransparencyData</c> and their user twins, as the
/// highlight and visualization state parsers read them.
/// </summary>
internal static class WiredVisualizationSerializer
{
    public static void SerializeHighlight(IServerPacket packet, WiredHighlightSnapshot highlight) =>
        packet
            .WriteInteger(highlight.Type)
            .WriteInteger(highlight.Strength)
            .WriteInteger(highlight.Color)
            .WriteInteger(highlight.AnimationMode)
            .WriteInteger(highlight.EasingMode);

    /// <summary>
    /// The transparencies (id, transparency, allow click through, easing), then the highlights
    /// (id, type, strength, colour, animation, easing).
    /// </summary>
    public static void SerializeState(
        IServerPacket packet,
        ImmutableArray<WiredTransparencyStateEntry> transparencies,
        ImmutableArray<WiredHighlightStateEntry> highlights
    )
    {
        packet.WriteInteger(transparencies.Length);

        foreach (var entry in transparencies)
            packet
                .WriteInteger(entry.Id)
                .WriteInteger(entry.Transparency.Transparency)
                .WriteBoolean(entry.Transparency.AllowClickThrough)
                .WriteInteger(entry.Transparency.EasingMode);

        packet.WriteInteger(highlights.Length);

        foreach (var entry in highlights)
        {
            packet.WriteInteger(entry.Id);
            SerializeHighlight(packet, entry.Highlight);
        }
    }
}
