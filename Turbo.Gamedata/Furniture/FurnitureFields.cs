using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// The furnidata fields a definition holds: what an import takes from Habbo and staff may edit.
/// The built file has its own order, Habbo's (<see cref="FurnitureDataWriter"/>). The item's identity (<c>id</c>, <c>classname</c>) is not among them, and nor
/// are its offers (<c>offerid</c>, <c>buyout</c>, <c>bc</c>, <c>bcofferid</c>, ...), which are
/// stamped from the catalog whenever the file is built (<see cref="FurnitureOfferStamps"/>).
/// </summary>
internal static class FurnitureFields
{
    public static readonly ImmutableArray<FurnitureField> All =
    [
        FurnitureField.Int("revision", e => e.Revision, (e, v) => e.Revision = v, clientOnly: true),
        FurnitureField.Text("category", 64, e => e.ClientCategory, (e, v) => e.ClientCategory = v),
        FurnitureField.Int(
            "defaultdir",
            e => e.DefaultDirection,
            (e, v) => e.DefaultDirection = v,
            floorOnly: true,
            clientOnly: true
        ),
        FurnitureField.Int("xdim", e => e.Width, (e, v) => e.Width = v, floorOnly: true),
        FurnitureField.Int("ydim", e => e.Length, (e, v) => e.Length = v, floorOnly: true),
        FurnitureField.Colors("partcolors", e => e.PartColors, (e, v) => e.PartColors = v),
        FurnitureField.Text("name", 255, e => e.PublicName, (e, v) => e.PublicName = v),
        FurnitureField.Text("description", null, e => e.Description, (e, v) => e.Description = v),
        FurnitureField.Text("adurl", 512, e => e.AdUrl, (e, v) => e.AdUrl = v),
        FurnitureField.Bool(
            "excludeddynamic",
            e => e.ExcludedDynamic,
            (e, v) => e.ExcludedDynamic = v,
            clientOnly: true
        ),
        FurnitureField.Text(
            "customparams",
            512,
            e => e.CustomParams,
            (e, v) => e.CustomParams = v,
            floorOnly: true
        ),
        FurnitureField.Int(
            "specialtype",
            e => (int)e.FurniCategory,
            (e, v) => e.FurniCategory = (FurnitureCategory)v
        ),
        FurnitureField.Bool("canstandon", e => e.CanWalk, (e, v) => e.CanWalk = v, floorOnly: true),
        FurnitureField.Bool("cansiton", e => e.CanSit, (e, v) => e.CanSit = v, floorOnly: true),
        FurnitureField.Bool("canlayon", e => e.CanLay, (e, v) => e.CanLay = v, floorOnly: true),
        FurnitureField.Bool(
            "canputstuffon",
            e => e.CanStack,
            (e, v) => e.CanStack = v,
            floorOnly: true
        ),
        // stack_height is double(10,4), the four decimals Habbo's heights use (0.0001, 1.1125).
        FurnitureField.Double(
            "height",
            4,
            e => e.StackHeight,
            (e, v) => e.StackHeight = v,
            floorOnly: true
        ),
        FurnitureField.Text("furniline", 64, e => e.FurniLine, (e, v) => e.FurniLine = v),
        FurnitureField.Text("environment", 64, e => e.Environment, (e, v) => e.Environment = v),
        FurnitureField.Bool("rare", e => e.Rare, (e, v) => e.Rare = v, clientOnly: true),
        FurnitureField.Bool("tradeable", e => e.CanTrade, (e, v) => e.CanTrade = v),
        FurnitureField.Bool("recyclable", e => e.CanRecycle, (e, v) => e.CanRecycle = v),
        // Not furnidata's: read from the furniture's asset file (HabboFurnitureFiles).
        FurnitureField.Int(
            "states",
            e => e.TotalStates,
            (e, v) => e.TotalStates = v,
            fromFile: true
        ),
    ];

    public static readonly FrozenDictionary<string, FurnitureField> ByKey = All.ToFrozenDictionary(
        x => x.Key
    );
}
