using System;
using System.Collections.Generic;
using Turbo.Database.Furniture;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Guilds;

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
    private static readonly Dictionary<string, string> FLOOR = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gate"] = "gate",
        ["pyramid"] = "gate",
        ["blackhole"] = "floor_hole",
        ["teleport"] = TeleportFurniture.LOGIC_NAME,
        ["teleporttile"] = TeleportFurniture.LOGIC_NAME,
        ["club_teleporttile"] = TeleportFurniture.LOGIC_NAME,
        ["dice"] = "dice",
        ["vendingmachine"] = FurnitureBehaviours.VENDING_MACHINE,
        ["vendingmachine_no_sides"] = FurnitureBehaviours.VENDING_MACHINE,
        ["effect_vendingmachine"] = FurnitureBehaviours.EFFECT_PROVIDER,
        ["effect_vendingmachine_no_sides"] = FurnitureBehaviours.EFFECT_PROVIDER,
        ["effect_giver"] = FurnitureBehaviours.EFFECT_PROVIDER,
        ["effect_tile"] = "effect_area",
        ["effect_tile_group"] = "effect_area",
        ["gym_equipment"] = "effect_area",
        ["fx_box"] = "effect_box",
        ["roller"] = "roller",
        ["trophy"] = TrophyData.LOGIC_NAME,
        ["badge_display"] = BadgeDisplayData.LOGIC_NAME,
        ["mannequin"] = "mannequin",
        ["ads_bg"] = "room_branding",
        ["background_toner"] = "background_toner",
        ["gift"] = PresentData.LOGIC_NAME,
        ["crackable"] = FurnitureBehaviours.CRACKABLE,
        ["crackable_master"] = FurnitureBehaviours.CRACKABLE,
        ["crackable_monster"] = FurnitureBehaviours.CRACKABLE,
        ["crackable_subscription_box"] = FurnitureBehaviours.CRACKABLE,
        ["love_lock"] = "love_lock",
        ["guild_furni"] = GuildFurnitureLogicNames.CUSTOMIZED,
        ["guild_gate"] = GuildFurnitureLogicNames.CUSTOMIZED,
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

        return DefaultFurnitureLogic.For(type);
    }

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

    private static bool Accepts(string logic, IReadOnlySet<string> registered) =>
        registered.Contains(logic);

    private static string Pick(string logic, IReadOnlySet<string> registered) =>
        Accepts(logic, registered) ? logic : DefaultFurnitureLogic.FLOOR;
}
