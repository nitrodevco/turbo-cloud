using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Import.Morningstar.Formats;

/// <summary>
/// Turbo's logic for a Morningstar <c>interaction_type</c>, for the definitions an import has to
/// create (furniture Turbo's own gamedata import did not bring: a hotel's custom furni). A
/// definition Turbo already has keeps its own logic. Wired boxes are named alike in both
/// emulators (Habbo's names), so a <c>wf_*</c> interaction is its own logic when Turbo registers
/// one under that name.
/// </summary>
internal static class InteractionLogic
{
    public const string DEFAULT_FLOOR = "default_floor";
    public const string DEFAULT_WALL = "default_wall";

    private static readonly Dictionary<string, string> FLOOR = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gate"] = "gate",
        ["pyramid"] = "gate",
        ["blackhole"] = "floor_hole",
        ["teleport"] = "teleport",
        ["teleporttile"] = "teleport",
        ["club_teleporttile"] = "teleport",
        ["dice"] = "dice",
        ["vendingmachine"] = "vending_machine",
        ["vendingmachine_no_sides"] = "vending_machine",
        ["effect_vendingmachine"] = "effect_provider",
        ["effect_vendingmachine_no_sides"] = "effect_provider",
        ["effect_giver"] = "effect_provider",
        ["effect_tile"] = "effect_area",
        ["effect_tile_group"] = "effect_area",
        ["gym_equipment"] = "effect_area",
        ["fx_box"] = "effect_box",
        ["roller"] = "roller",
        ["trophy"] = "trophy",
        ["badge_display"] = "badge_display",
        ["mannequin"] = "mannequin",
        ["ads_bg"] = "room_branding",
        ["background_toner"] = "background_toner",
        ["gift"] = "present",
        ["crackable"] = "crackable",
        ["crackable_master"] = "crackable",
        ["crackable_monster"] = "crackable",
        ["crackable_subscription_box"] = "crackable",
        ["love_lock"] = "love_lock",
        ["guild_furni"] = "guild_customized",
        ["guild_gate"] = "guild_customized",
        ["jukebox"] = "jukebox",
        ["musicdisc"] = "song_disk",
        ["monsterplant_seed"] = "monsterplant_seed",
        ["nest"] = "pet_nest",
        ["pet_drink"] = "pet_drink",
        ["pet_food"] = "pet_food",
        ["pet_toy"] = "pet_toy",
        ["breeding_nest"] = "pet_breeding_nest",
        ["onewaygate"] = "one_way_door",
        ["stack_helper"] = "custom_stack_height",
        ["tile_walkmagic"] = "custom_stack_height",
        ["random_state"] = "random_state",
        ["battlebanzai_random_teleport"] = "random_teleport",
        ["game_timer"] = "game_timer",
        ["battlebanzai_tile"] = "battlebanzai_tile",
        ["battlebanzai_puck"] = "battlebanzai_puck",
        ["wf_highscore"] = "highscore",
        ["clothing"] = "purchasable_clothing",
        ["youtube"] = "youtube",
        ["rentable_space"] = "rentable_space",
    };

    private static readonly Dictionary<string, string> WALL = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dimmer"] = "dimmer",
        ["postit"] = "stickie",
    };

    /// <summary>The logic a new definition of this kind gets.</summary>
    public static string For(string interaction, ProductType type, IReadOnlySet<string> registered)
    {
        var map = type == ProductType.Wall ? WALL : FLOOR;

        if (map.TryGetValue(interaction, out var logic) && Accepts(logic, registered))
            return logic;

        if (type == ProductType.Floor)
        {
            if (
                interaction.StartsWith("battlebanzai_gate_", StringComparison.OrdinalIgnoreCase)
                || interaction.StartsWith("freeze_gate_", StringComparison.OrdinalIgnoreCase)
            )
                return Pick("game_team_gate", registered);

            if (interaction.Contains("_counter_", StringComparison.OrdinalIgnoreCase))
                return Pick("game_team_score", registered);

            // Wired boxes carry Habbo's names in both emulators.
            if (
                interaction.StartsWith("wf_", StringComparison.OrdinalIgnoreCase)
                && registered.Contains(interaction.ToLowerInvariant())
            )
                return interaction.ToLowerInvariant();
        }

        return type == ProductType.Wall ? DEFAULT_WALL : DEFAULT_FLOOR;
    }

    /// <summary>
    /// The logic a definition really runs: Turbo swaps a <c>default_floor</c> or unregistered
    /// logic for the classname when that is a registered wired key (<c>RoomObjectLogicProvider</c>).
    /// </summary>
    public static string Effective(string logic, string name, IReadOnlySet<string> registered) =>
        (logic is DEFAULT_FLOOR or "none" || !registered.Contains(logic))
        && name.StartsWith("wf_", StringComparison.Ordinal)
        && registered.Contains(name)
            ? name
            : logic;

    /// <summary>The furnidata special type a new definition of this kind gets.</summary>
    public static FurnitureCategory Category(string interaction, string name, ProductType type) =>
        interaction.ToLowerInvariant() switch
        {
            "gift" => FurnitureCategory.Present,
            "trophy" => FurnitureCategory.Trophy,
            "postit" when type == ProductType.Wall => FurnitureCategory.PostIt,
            "musicdisc" => FurnitureCategory.TraxSong,
            "guild_furni" or "guild_gate" => FurnitureCategory.GuildFurni,
            "clothing" => FurnitureCategory.FigurePurchasableSet,
            "monsterplant_seed" => FurnitureCategory.MonsterplantSeed,
            _ => name.ToLowerInvariant() switch
            {
                "wallpaper" => FurnitureCategory.WallPaper,
                "floor" => FurnitureCategory.Floor,
                "landscape" => FurnitureCategory.Landscape,
                "poster" => FurnitureCategory.Poster,
                _ => FurnitureCategory.Default,
            },
        };

    /// <summary>Every logic key the given assemblies register (<c>[RoomObjectLogic]</c>).</summary>
    public static IReadOnlySet<string> Registered(params Assembly[] assemblies) =>
        assemblies
            .SelectMany(SafeTypes)
            .Select(t => t.GetCustomAttribute<RoomObjectLogicAttribute>()?.Key)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static bool Accepts(string logic, IReadOnlySet<string> registered) =>
        registered.Contains(logic);

    private static string Pick(string logic, IReadOnlySet<string> registered) =>
        Accepts(logic, registered) ? logic : DEFAULT_FLOOR;

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
