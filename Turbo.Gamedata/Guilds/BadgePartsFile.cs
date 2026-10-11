using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Turbo.Database.Entities.Guilds;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Gamedata.Guilds;

/// <summary>
/// The group badge parts and the badge palette, as the imager that draws badges reads them: what
/// every id in a badge code means. The editor is sent the same rows in <c>GuildEditorData</c> and
/// the codes are built from them, so the imager reading this file draws exactly what was picked,
/// and a colour retuned by staff reaches it with the next build.
/// <code>
/// {
///   "bases":   [ { "id": 1, "fileName": "base_basic_1", "maskFileName": "" }, ... ],
///   "symbols": [ { "id": 1, "fileName": "symbol_0", "maskFileName": "" }, ... ],
///   "colors":  [ { "id": 1, "color": "EEEEEE" }, ... ]
/// }
/// </code>
/// Only the badge palette is written: the primary and secondary colours recolour furni, not badges.
/// </summary>
internal static class BadgePartsFile
{
    private static readonly JsonWriterOptions WRITER = new() { Indented = true };

    public static byte[] Write(
        IEnumerable<GuildBadgePartEntity> parts,
        IEnumerable<GuildColorEntity> colors
    )
    {
        var byType = parts.OrderBy(x => x.PartId).ToLookup(x => x.PartType);

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, WRITER))
        {
            writer.WriteStartObject();

            WriteParts(writer, "bases", byType[GuildBadgePartType.Base]);
            WriteParts(writer, "symbols", byType[GuildBadgePartType.Symbol]);

            writer.WriteStartArray("colors");

            foreach (
                var color in colors
                    .Where(x => x.Slot == GuildColorSlotType.Badge)
                    .OrderBy(x => x.ColorId)
            )
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", color.ColorId);
                writer.WriteString("color", color.Color);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteParts(
        Utf8JsonWriter writer,
        string name,
        IEnumerable<GuildBadgePartEntity> parts
    )
    {
        writer.WriteStartArray(name);

        foreach (var part in parts)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", part.PartId);
            writer.WriteString("fileName", part.FileName);
            writer.WriteString("maskFileName", part.MaskFileName);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
