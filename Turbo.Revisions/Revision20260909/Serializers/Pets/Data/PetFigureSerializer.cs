using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

/// <summary>The pet figure struct: type, palette, colour, breed, then the custom part triples.</summary>
internal static class PetFigureSerializer
{
    public static void Serialize(IServerPacket packet, PetFigureSnapshot figure)
    {
        packet
            .WriteInteger(figure.TypeId)
            .WriteInteger(figure.PaletteId)
            .WriteString(figure.Color)
            .WriteInteger(figure.BreedId)
            .WriteInteger(figure.CustomParts.Length / PetFigure.CUSTOM_PART_FIELDS);

        foreach (var value in figure.CustomParts)
            packet.WriteInteger(value);
    }
}
