using System.Globalization;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Furni whose height changes with their state: the Builders Club building blocks (Large Block,
/// Panel Block, Stick Block...). Their assets have the logic type <c>furniture_multiheight</c>
/// (Flash <c>FurnitureMultiHeightLogic</c>: a multi-state furni of variable height), a frame per
/// state drawing the block lower each time, and their furnidata gives the step down per state as
/// a negative <c>customparams</c>: a quarter of the height (block -0.25 of 1, panel -0.5 of 2.01,
/// stick -1.00 of 4), so the last of their five states lies flat.
/// </summary>
public static class MultiHeightFurniture
{
    public const string LOGIC_NAME = "furniture_multiheight";

    /// <summary>How much lower each state stands, or null for a furni that is not one of these.</summary>
    public static double? StepOf(FurnitureDefinitionSnapshot definition) =>
        double.TryParse(
            definition.CustomParams,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var step
        )
        && step < 0
        && definition.TotalStates > 1
            ? -step
            : null;
}
