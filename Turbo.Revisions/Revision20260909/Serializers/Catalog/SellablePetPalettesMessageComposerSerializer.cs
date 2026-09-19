using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class SellablePetPalettesMessageComposerSerializer(int header)
    : AbstractSerializer<SellablePetPalettesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SellablePetPalettesMessageComposer message
    )
    {
        packet.WriteString(message.ProductCode).WriteInteger(message.Palettes.Length);

        foreach (var palette in message.Palettes)
        {
            packet
                .WriteInteger(palette.TypeId)
                .WriteInteger(palette.BreedId)
                .WriteInteger(palette.PaletteId)
                .WriteBoolean(palette.Sellable)
                .WriteBoolean(palette.Rare);
        }
    }
}
