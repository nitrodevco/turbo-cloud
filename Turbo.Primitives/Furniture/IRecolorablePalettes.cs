using System.Collections.Generic;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// The colours each recolorable furni may take, from the hotel's settings
/// (<see cref="RecolorableData.GetPalette"/> over <c>RecolorablePaletteToFurni</c> and
/// <c>RecolorablePalettes</c>): what the recolor window and the catalog's
/// <c>recolorable_furniture</c> page offer.
/// </summary>
public interface IRecolorablePalettes
{
    /// <summary>
    /// The palette of a furni definition, by its name (<c>bc_block_0_recolorable</c>, or a colour
    /// variant <c>name*N</c>), in palette order; empty when its palette is not set.
    /// </summary>
    IReadOnlyList<int> PaletteOf(string definitionName);
}
