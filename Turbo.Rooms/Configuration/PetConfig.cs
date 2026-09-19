using System.Collections.Generic;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Rooms.Configuration;

/// <summary>Tunables of pets standing in rooms: stats, growth, commands and their words.</summary>
public class PetConfig
{
    public const string SECTION_NAME = "Turbo:Pets";

    public int MaxPetsPerRoom { get; init; } = 15;
    public int NameMinLength { get; init; } = 1;
    public int NameMaxLength { get; init; } = 15;
    public int MaxEnergy { get; init; } = 100;
    public int MaxNutrition { get; init; } = 100;
    public int MaxLevel { get; init; } = 20;

    /// <summary>Experience needed to reach level 2, 3, ... in order.</summary>
    public int[] LevelExperienceThresholds { get; init; } =
    [
        100,
        200,
        400,
        600,
        1000,
        1300,
        1800,
        2400,
        3200,
        4300,
        7200,
        8500,
        10100,
        13300,
        17500,
        23000,
        51900,
        120000,
        240000,
        360000,
    ];

    /// <summary>Levels at which the horse skill bar marks a threshold.</summary>
    public int[] SkillThresholdLevels { get; init; } = [];

    /// <summary>Experience for obeying a command, and for being scratched.</summary>
    public int CommandExperience { get; init; } = 10;
    public int RespectExperience { get; init; } = 10;
    public int CommandEnergyCost { get; init; } = 3;
    public int CommandNutritionCost { get; init; } = 1;

    /// <summary>How long an action posture (beg, jump, play) lasts before the pet stands again.</summary>
    public int ActionDurationMs { get; init; } = 5000;
    public int SpeakDurationMs { get; init; } = 2000;
    public int IdleActionMinMs { get; init; } = 4000;
    public int IdleActionMaxMs { get; init; } = 12000;
    public int FreeRoamWalkChancePercent { get; init; } = 60;
    public int FreeRoamMaxDistance { get; init; } = 6;
    public int EnergyDecayMs { get; init; } = 120000;
    public int NutritionDecayMs { get; init; } = 180000;
    public int RestEnergyPerTick { get; init; } = 2;
    public int FoodNutrition { get; init; } = 20;
    public int DrinkEnergy { get; init; } = 20;
    public int ToyExperience { get; init; } = 5;
    public int HandItemNutrition { get; init; } = 10;
    public int HandItemEnergy { get; init; } = 10;
    public int HandItemEatDurationMs { get; init; } = 3000;

    /// <summary>Energy below which a free-roaming pet looks for food, drink or a nest on its own.</summary>
    public int HungryNutrition { get; init; } = 30;
    public int TiredEnergy { get; init; } = 20;

    /// <summary>Days an account must be old before it may scratch a pet.</summary>
    public int RespectMinAccountAgeDays { get; init; } = 0;

    public int MonsterplantMaxLevel { get; init; } = 7;
    public int MonsterplantGrowthSeconds { get; init; } = 3600;
    public int MonsterplantWellBeingSeconds { get; init; } = 259200;
    public int MonsterplantHarvestIntervalSeconds { get; init; } = 86400;
    public int MonsterplantSupplementEnergy { get; init; } = 20;

    /// <summary>Name a freshly planted monsterplant gets.</summary>
    public string MonsterplantDefaultName { get; init; } = "Monsterplant";

    /// <summary>Definition (class) name of the seed a harvested or bred monsterplant yields.</summary>
    public string MonsterplantSeedDefinitionName { get; init; } = "mnstr_seed";

    /// <summary>Definition (class) name of the saddle returned when it is taken off a horse.</summary>
    public string SaddleDefinitionName { get; init; } = "horse_saddle1";

    /// <summary>Chance in percent per rarity level (index) when a nest breeding picks a breed.</summary>
    public int[] BreedingRarityChances { get; init; } = [70, 20, 7, 3];
    public int NestBreedingMinLevel { get; init; } = 1;

    /// <summary>Lines a pet may say when told to speak, or on its own now and then.</summary>
    public string[] SpeechLines { get; init; } = ["Woof!", "Arf!", "..."];

    /// <summary>Level from which each command is understood; unlisted commands are never enabled.</summary>
    public Dictionary<PetCommandType, int> CommandUnlockLevels { get; init; } =
        new()
        {
            [PetCommandType.Free] = 1,
            [PetCommandType.Sit] = 1,
            [PetCommandType.ComeHere] = 1,
            [PetCommandType.Stand] = 1,
            [PetCommandType.Breed] = 1,
            [PetCommandType.LieDown] = 2,
            [PetCommandType.Stay] = 2,
            [PetCommandType.Beg] = 3,
            [PetCommandType.Follow] = 3,
            [PetCommandType.PlayDead] = 4,
            [PetCommandType.Jump] = 5,
            [PetCommandType.Speak] = 6,
            [PetCommandType.Play] = 7,
            [PetCommandType.Silent] = 8,
            [PetCommandType.Nest] = 9,
            [PetCommandType.Drink] = 10,
            [PetCommandType.FollowLeft] = 11,
            [PetCommandType.FollowRight] = 11,
            [PetCommandType.PlayFootball] = 12,
        };

    /// <summary>
    /// The words a player says after the pet's name to give each command. The client's buttons
    /// say the localized <c>pet.command.&lt;id&gt;</c> text, so these must match the hotel's texts.
    /// </summary>
    public Dictionary<PetCommandType, string[]> CommandWords { get; init; } =
        new()
        {
            [PetCommandType.Free] = ["free"],
            [PetCommandType.Sit] = ["sit"],
            [PetCommandType.LieDown] = ["lie down", "down", "lay"],
            [PetCommandType.ComeHere] = ["come here", "here", "come"],
            [PetCommandType.Beg] = ["beg"],
            [PetCommandType.PlayDead] = ["play dead", "dead"],
            [PetCommandType.Stay] = ["stay"],
            [PetCommandType.Follow] = ["follow"],
            [PetCommandType.Stand] = ["stand", "stand up"],
            [PetCommandType.Jump] = ["jump"],
            [PetCommandType.Speak] = ["speak", "talk"],
            [PetCommandType.Play] = ["play"],
            [PetCommandType.Silent] = ["silent", "quiet"],
            [PetCommandType.Nest] = ["nest"],
            [PetCommandType.Drink] = ["drink"],
            [PetCommandType.FollowLeft] = ["follow left"],
            [PetCommandType.FollowRight] = ["follow right"],
            [PetCommandType.PlayFootball] = ["play football", "football"],
            [PetCommandType.Breed] = ["breed"],
        };
}
