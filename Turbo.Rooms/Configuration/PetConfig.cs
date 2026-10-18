using System.Collections.Generic;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Object;

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

    /// <summary>
    /// What a free-roaming pet of a type does when it stops, by pet type: a posture ("eyb",
    /// "kck") or a gesture ("gst sml") held for <see cref="IdlePostureDurationMs"/>, or "sit" /
    /// "lay" to rest, or "std" to just stand. A type not listed sits, lies, wags its tail, speaks
    /// or looks round. An action may end in ":&lt;weight&gt;" to be picked that much more often
    /// (default 1). Horses from the official client capture of 2026-10-17 (room "saddle club", two
    /// horses, 540 s, 891 updates), weighted by how many times each was seen: stand, blink, smile
    /// (the heart), kick a front leg, wag, lie down (unsaddled only); never sit (the horse has no
    /// sit posture). The thirst, tired, playful and hungry gestures are need icons (the horse's
    /// drop, house, ball and cutlery), not idle filler: none were seen there.
    /// </summary>
    public Dictionary<int, string[]> IdleActionsByType { get; init; } =
        new() { [15] = ["std:88", "eyb:51", "gst sml:47", "kck:22", "wag:17", "lay:8"] };

    /// <summary>How long an idle posture or gesture lasts: an official horse blinks for 1.0 s.</summary>
    public int IdlePostureDurationMs { get; init; } = 1000;

    /// <summary>
    /// Whether a saddled pet sits or lies down, on its own or when told. Official saddled horses
    /// never did (0 of ~330 updates) and the horse has no saddle drawn lying down.
    /// </summary>
    public bool SaddledPetsRest { get; init; } = false;

    /// <summary>
    /// Chance in percent that a pet drinking, eating or playing with a toy says one of the
    /// hotel's <c>pet.vocals.&lt;type&gt;.DRINKING</c> / <c>EATING</c> / <c>PLAYING</c> lines.
    /// </summary>
    public int VocalChancePercent { get; init; } = 100;

    /// <summary>
    /// How high a pet sits on a seat it walked onto, whatever the seat. Official free pets
    /// (types 12, 17, 37) on sofas of height 1.0 sat at 0.45 while players on the same sofas sat
    /// at 1.0 (official client capture 2026-10-18, 10 of 10 seat visits).
    /// </summary>
    public Altitude PetSeatHeight { get; init; } = Altitude.FromValue(0.45);

    /// <summary>
    /// Chance in percent that a free-roaming pet, when it next decides what to do, walks to a toy
    /// in the room and plays with it. Two official horses with a rubber ball played 6 times in
    /// 9 minutes (official client capture 2026-10-17, room "saddle club"): about one decision in
    /// twenty at this pace.
    /// </summary>
    public int ToyPlayChancePercent { get; init; } = 5;

    /// <summary>
    /// How long a pet plays with a toy, and the toy shows it is played with (state 1): 3.5 s,
    /// 6 of 6 official plays.
    /// </summary>
    public int ToyPlayDurationMs { get; init; } = 3500;

    /// <summary>
    /// The posture a pet of a type plays with a toy in, where it is not "pla": an official horse
    /// kicks the ball with a front leg ("kck", 6 of 6; the horse has no play posture).
    /// </summary>
    public Dictionary<int, string> ToyPlayPostureByType { get; init; } = new() { [15] = "kck" };

    /// <summary>
    /// Chance in percent that a free-roaming pet, when it next decides what to do, goes for a
    /// drink however rested it is. Two official horses at two troughs and a water bowl drank 9
    /// times in 9 minutes, 45-150 s apart, with their energy above its maximum the whole visit
    /// (official client capture 2026-10-17, room "saddle club"): about one decision in twelve at
    /// this pace.
    /// </summary>
    public int DrinkChancePercent { get; init; } = 8;

    /// <summary>
    /// Supplies a free pet walks to with a smile ("gst sml"), held from the moment it sets off
    /// until it gets there: official horses did on the way to every drink (9 of 9) and every toy
    /// (6 of 6).
    /// </summary>
    public PetSupplyType[] SmileOnTheWayTo { get; init; } =
    [PetSupplyType.Drink, PetSupplyType.Toy];

    /// <summary>
    /// The icon ("gst &lt;code&gt;") a pet shows for a need nothing in the room answers: thirsty
    /// when it goes for a drink and there is none, playful when it goes to play and there is no
    /// toy, tired (instead of its smile) while it is tired with no nest or drink to go to, and
    /// hungry (instead of its smile) while it is hungry with no food. Empty or missing: no icon.
    /// The horse draws these as a water drop, a ball, a house and a knife and fork. Official
    /// horses showed thr, plf and trd in rooms where they never drank, and none in 9 minutes in
    /// a stable with troughs, a water bowl and a ball (official client captures 2026-10-17). No
    /// recording has ever shown hng, while every official pet looked at had 0-7 of 100 nutrition,
    /// so hungry has no icon by default.
    /// </summary>
    public Dictionary<PetNeed, string> NeedGestures { get; init; } =
        new()
        {
            [PetNeed.Thirsty] = "thr",
            [PetNeed.Playful] = "plf",
            [PetNeed.Tired] = "trd",
            [PetNeed.Hungry] = "",
        };

    public int FreeRoamMaxDistance { get; init; } = 6;
    public int EnergyDecayMs { get; init; } = 120000;
    public int NutritionDecayMs { get; init; } = 180000;
    public int RestEnergyPerTick { get; init; } = 2;
    public int FoodNutrition { get; init; } = 20;

    /// <summary>
    /// Nutrition and energy a drink gives. Official drinks raised the horse's nutrition by one
    /// each and left its energy as it was (PetInfo before and after 3 drinks, official client
    /// capture 2026-10-17, room "saddle club").
    /// </summary>
    public int DrinkNutrition { get; init; } = 1;
    public int DrinkEnergy { get; init; } = 0;

    /// <summary>
    /// A drink, as the official horses drank (9 of 9): the pet stands at the drink with a smile
    /// for <see cref="DrinkStartDelayMs"/>, drinks ("eat", no gesture) for
    /// <see cref="DrinkDurationMs"/>, then smiles again as the drink goes down a level and stands
    /// for <see cref="DrinkEndMs"/> before it moves on (1 s, 3.5 s and 0.5 s).
    /// </summary>
    public int DrinkStartDelayMs { get; init; } = 1000;
    public int DrinkDurationMs { get; init; } = 3500;
    public int DrinkEndMs { get; init; } = 500;

    /// <summary>
    /// How long a food or drink has to be left alone to be full again at its next serving; one not
    /// served since the room loaded is full too. Official: the first drink at each of a stable's
    /// three (left at 4, 2 and 3) showed it full less one (4 of 5), and 205 s between two drinks
    /// did not refill one (official client capture 2026-10-17, room "saddle club"); the period
    /// itself is unrecorded, 5 minutes is an inference. 0: only a room load refills.
    /// </summary>
    public int SupplyRefillMs { get; init; } = 300000;

    /// <summary>
    /// How long a free pet stands with a smile before it sets off for a drink or a toy, turning to
    /// it halfway: official horses showed "std/gst sml" for 2 s, turned after 1 s, and took
    /// their first step 2.5 s after the smile began (7 of 7 set-offs, room "saddle club").
    /// </summary>
    public int SetOffLeadInMs { get; init; } = 2500;
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

    /// <summary>
    /// Seconds a monsterplant takes to grow a level: 8 hours, so its six level-ups take the two
    /// days a plant grows (Wired Faculty tutorial "Monster Plant growth time FX", 23/09/2026).
    /// </summary>
    public int MonsterplantGrowthSeconds { get; init; } = 28800;

    /// <summary>A grown monsterplant's wellbeing when watered: 72 hours.</summary>
    public int MonsterplantWellBeingSeconds { get; init; } = 259200;

    /// <summary>
    /// A monsterplant's wellbeing while it is still growing: 36 hours (Wired Faculty tutorial "How
    /// to Show a Health Bar Above Your Monster Plants", 15/09/2026: ~plant.max_wellbeing_seconds is
    /// 36 hours for a baby and 72 for an adult).
    /// </summary>
    public int MonsterplantBabyWellBeingSeconds { get; init; } = 129600;
    public int MonsterplantHarvestIntervalSeconds { get; init; } = 86400;
    public int MonsterplantSupplementEnergy { get; init; } = 20;

    /// <summary>Name a freshly planted monsterplant gets.</summary>
    public string MonsterplantDefaultName { get; init; } = "Monsterplant";

    /// <summary>Definition (class) name of the seed a harvested or bred monsterplant yields.</summary>
    public string MonsterplantSeedDefinitionName { get; init; } = "mnstr_seed";

    /// <summary>Definition (class) name of the saddle returned when it is taken off a horse.</summary>
    public string SaddleDefinitionName { get; init; } = "horse_saddle1";

    /// <summary>
    /// How far above its horse a rider stands. The client draws the rider wherever the room says
    /// (its riding effect only swaps the legs and adds a saddle at standing height), so the server
    /// alone lifts the rider onto the horse's back. One height unit is inferred from where the
    /// riding effect draws its saddle against the horse's own; no official recording of a rider yet.
    /// </summary>
    public Altitude RiderHeight { get; init; } = Altitude.FromValue(1.0);

    /// <summary>Chance in percent per rarity level (index) when a nest breeding picks a breed.</summary>
    public int[] BreedingRarityChances { get; init; } = [70, 20, 7, 3];
    public int NestBreedingMinLevel { get; init; } = 1;

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
