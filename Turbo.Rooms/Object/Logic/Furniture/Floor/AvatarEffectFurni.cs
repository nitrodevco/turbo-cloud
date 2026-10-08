using System.Globalization;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The avatar effect a furni puts on: its definition's <c>customparams</c>, which Habbo's furni
/// data gives every effect furni (<c>fxbox_fx116</c> 116, the Habbo Gun Vender 182, the Builders
/// Club water block 30) and the hotel's furnidata carries into the definition.
/// </summary>
internal static class AvatarEffectFurni
{
    /// <summary>
    /// The effect id, or null when the definition names none. Zero is an effect too: no effect,
    /// which the new-user room's <c>room_noob_fxremove</c> tile puts on to take one off.
    /// </summary>
    public static int? EffectIdOf(FurnitureDefinitionSnapshot definition)
    {
        var first = (definition.CustomParams ?? string.Empty).Split(',')[0].Trim();

        return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }
}
