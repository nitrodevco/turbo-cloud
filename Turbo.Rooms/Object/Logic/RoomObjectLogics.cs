using System;
using System.Collections.Generic;
using System.Reflection;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Games;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Highscore;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Object.Logic;

/// <summary>
/// Which furni logic exists, and which one a definition runs. The room registers the classes
/// <see cref="In"/> finds and makes each item's logic from <see cref="Resolve"/>; the Morningstar
/// import asks both questions of its definitions too (<c>ImportStartup</c>), so an imported item's
/// data is written for the logic its room will give it.
/// </summary>
public static class RoomObjectLogics
{
    private const string WIRED_CLASSNAME_PREFIX = "wf_";

    /// <summary>The <c>[RoomObjectLogic]</c> classes in an assembly, with the key each registers under.</summary>
    public static IEnumerable<(Type Type, string Key)> In(Assembly asm)
    {
        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IRoomObjectLogic)))
            if (concrete?.GetCustomAttribute<RoomObjectLogicAttribute>(false) is { } attribute)
                yield return (concrete, attribute.Key);
    }

    /// <summary>
    /// The logic a definition runs: the one its <c>logic</c> column names
    /// (<paramref name="logicType"/>), unless the definition says more. Plain furniture becomes a
    /// water area named in <paramref name="waterLogicByDefinition"/>, a recolorable block by its
    /// class, or a multi-height block by its custom params. Plain furniture, or a logic nothing
    /// registered (<paramref name="isRegistered"/>), becomes a game timer, game furni (team gates,
    /// scoreboards, Banzai tiles), clothing, a highscore board or a registered wired box, by its
    /// classname or furnidata category. Each step sees what the step before it chose.
    /// </summary>
    public static string Resolve(
        string logicType,
        FurnitureDefinitionSnapshot definition,
        Func<string, bool> isRegistered,
        IReadOnlyDictionary<string, string> waterLogicByDefinition
    )
    {
        if (
            logicType == DefaultFurnitureLogic.FLOOR
            && waterLogicByDefinition.TryGetValue(definition.Name, out var waterLogic)
        )
            logicType = waterLogic;

        // Habbo's recolorable furni (the 2026-10 building and alphabet blocks) are known by their
        // classnames, so a definition imported after MapRecolorableFurnitureLogic ran still keeps
        // a colour; their multi-height blocks step down in that logic too.
        if (
            logicType == DefaultFurnitureLogic.FLOOR
            && RecolorableData.IsRecolorableClass(definition.Name)
        )
            logicType = RecolorableData.LOGIC_NAME;

        // A building block's height follows its state; its definition says so only through
        // its customparams (the step down per state), its logic column being the default.
        if (
            logicType == DefaultFurnitureLogic.FLOOR
            && MultiHeightFurniture.StepOf(definition) is not null
        )
            logicType = MultiHeightFurniture.LOGIC_NAME;

        // The game timers (Banzai, Football, Freeze counters) are known by their classnames.
        if (
            Replaceable(logicType, isRegistered)
            && Array.IndexOf(FurnitureGameTimerLogic.CLASSNAMES, definition.Name) >= 0
        )
            logicType = FurnitureGameTimerLogic.LOGIC_NAME;

        // So are the team gates, team scoreboards and Banzai tiles (GameFurniture).
        if (
            Replaceable(logicType, isRegistered)
            && GameFurniture.LogicOf(definition.Name) is { } game
        )
            logicType = game;

        // Clothing furni are known by their furnidata specialtype (figure_purchasable_set), which
        // hotels set without a logic of their own.
        if (
            Replaceable(logicType, isRegistered)
            && definition.FurniCategory == FurnitureCategory.FigurePurchasableSet
        )
            logicType = FurniturePurchasableClothingLogic.LOGIC_NAME;

        // A highscore board is known by its classname (highscore_perteam*2 and so on).
        if (
            Replaceable(logicType, isRegistered)
            && HighscoreBoards.TryParse(definition.Name, out _, out _)
        )
            logicType = HighscoreBoards.LOGIC_NAME;

        // A wired box's logic is its classname. A definition whose logic column names something
        // else (left at the default, or a name never registered) would make the box plain
        // furniture that never opens its editor, so the classname wins for wired boxes.
        if (
            Replaceable(logicType, isRegistered)
            && definition.Name.StartsWith(WIRED_CLASSNAME_PREFIX, StringComparison.Ordinal)
            && isRegistered(definition.Name)
        )
            logicType = definition.Name;

        return logicType;
    }

    private static bool Replaceable(string logicType, Func<string, bool> isRegistered) =>
        logicType == DefaultFurnitureLogic.FLOOR || !isRegistered(logicType);
}
