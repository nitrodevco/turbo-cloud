using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Turbo.Database.Furniture.Defaults;

namespace Turbo.Database.Furniture;

/// <summary>
/// The behaviour Habbo's furni have in Sulake's furni data (github 80O/FurniData: the XML data
/// to 2021, the Unity data since), by classname, which a hotel otherwise has to set by hand:
/// every new item is plain furniture until staff give it a logic. The import gives a new
/// definition its behaviour from here (<c>FurnitureBehaviourApplier</c> in Turbo.Gamedata), and
/// <c>MapFurnitureBehaviours</c> gave it to the definitions a hotel had before.
/// <list type="bullet">
/// <item><c>crackable</c>: the six crackable classes. Each gets its asset's state count, its usage
/// (<c>&lt;everyone-can-use/&gt;</c> makes it everyone's, else room rights), whether it is walked on
/// (<c>&lt;stand/&gt;</c>: piñatas), and a <c>crackable</c> section from its product parameter and
/// class: the hits that crack it, the effect a hit needs, its hit achievements, who gets what
/// is inside (whoever cracks a <c>Public</c> one, else the owner) and where it goes (the
/// inventory for <c>CrackableInventoryProductRewardFurniture</c>, else the tile). What each
/// holds is not in Sulake's data: the Habbo Club and Builders Club boxes hold 14 or 31 days,
/// the rest what the Habbox Wiki's campaign pages, their illustrations and the hotel's own
/// texts say, with their odds where given and equal chances where not. Growth and evolution
/// chains give the next stage; some give several things at once (<c>draws</c>). One with no
/// known contents takes hits but is never cracked.</item>
/// <item><c>vending_machine</c>: <c>VendingMachineFurni</c>, <c>IceCreamMachineFurni</c> (both
/// animate state 1) and <c>HandItemProviderFurni</c> (static), with the hand items their
/// <c>&lt;drinks&gt;</c> list; for those newer than the XML data, an NFT copy's original's, the
/// hotel's own <c>handitemN</c> name for an item added with it, or the Habbox Wiki's list. All
/// everyone's, as 277 of the 280 in the XML data are.</item>
/// <item><c>effect_box</c>, <c>effect_provider</c> (everyone's), <c>effect_area</c>,
/// <c>effect_tile</c>, <c>random_teleport</c> (the Banzai teleporter) and <c>floor_hole</c> (the
/// Black Hole), by class.</item>
/// </list>
/// A furni the hotel only has as colour variants (<c>rare_icecream*0</c> to <c>*12</c>) is matched
/// by the name before its <c>*</c>. Only a definition still on a default logic, or already on the
/// one given, is changed, so applying it again changes nothing and staff choices stand.
/// </summary>
public static class FurnitureBehaviours
{
    public const string CRACKABLE = FurnitureBehaviourDefaults20261008.CRACKABLE;
    public const string VENDING_MACHINE = FurnitureBehaviourDefaults20261008.VENDING_MACHINE;
    public const string EFFECT_PROVIDER = FurnitureBehaviourDefaults20261008.EFFECT_PROVIDER;

    /// <summary>The classname before its colour variant mark (<c>rare_icecream*3</c> is <c>rare_icecream</c>).</summary>
    public static string BaseName(string className)
    {
        var mark = className.IndexOf('*', StringComparison.Ordinal);

        return mark > 0 ? className[..mark] : className;
    }

    /// <summary>Whether a definition on this logic may take the behaviour: the defaults, or the behaviour's own.</summary>
    public static bool MayTake(string logic, string behaviourLogic) =>
        logic is "default_floor" or "none" or "" || logic == behaviourLogic;

    public static readonly FrozenDictionary<
        string,
        (int States, int Usage, bool Walkable, string Crackable)
    > CrackablesByName = FurnitureBehaviourDefaults20261008
        .Crackables.ToDictionary(
            x => x.Name,
            x => (x.States, x.Usage, x.Walkable, x.Crackable),
            StringComparer.Ordinal
        )
        .ToFrozenDictionary(StringComparer.Ordinal);

    public static readonly FrozenDictionary<string, (int Usage, string Vending)> VendingByName =
        FurnitureVendingDefaults20261008
            .Entries.ToDictionary(x => x.Name, x => (x.Usage, x.Vending), StringComparer.Ordinal)
            .ToFrozenDictionary(StringComparer.Ordinal);

    public static readonly FrozenDictionary<string, string> LogicsByName =
        FurnitureBehaviourDefaults20261008
            .Logics.ToDictionary(x => x.Name, x => x.Logic, StringComparer.Ordinal)
            .ToFrozenDictionary(StringComparer.Ordinal);
}
