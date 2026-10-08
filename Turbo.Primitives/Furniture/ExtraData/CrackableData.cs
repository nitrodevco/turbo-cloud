using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// How a crackable furni opens, under <see cref="SECTION"/> in its definition's extra data.
/// <para>
/// It carries what Sulake's furni data gives each crackable as its product parameter
/// (<c>{"rewardSet":"bcgift_1","target":"1"}</c>, <c>requiredEffectId</c>,
/// <c>incrementalHitAchievementName</c>, <c>finalHitAchievementName</c>,
/// <c>finalHitAchievementCount</c>), and what the server class it names
/// (<c>CrackableRewardFurniture</c>, <c>PublicCrackableProductRewardFurniture</c>,
/// <c>CrackableInventoryProductRewardFurniture</c>, <c>EffectDependentCrackableRuntime</c>,
/// <c>PinataFurniture</c>) decides: how it is hit, who gets what is inside and where it goes.
/// Who may hit it is not here: it is the definition's usage policy, as Sulake's data gives it
/// (<c>&lt;everyone-can-use/&gt;</c> on eggs, crystals, piñatas and some plants; room rights for
/// the rest).
/// A reward set's contents are not in Sulake's data, so a hotel lists them here
/// (<see cref="Rewards"/>); a crackable with none is never opened - it keeps its last hit rather
/// than vanish for nothing.
/// </para>
/// </summary>
public sealed record CrackableData
{
    public const string SECTION = "crackable";

    /// <summary>The reward set's name in Sulake's data, for the logs and for whoever fills <see cref="Rewards"/>.</summary>
    public string? RewardSet { get; init; }

    /// <summary>The hits that crack it (Sulake's <c>target</c>); at least one.</summary>
    public int Target { get; init; } = 1;

    /// <summary>What lands a hit.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CrackableHitOn HitOn { get; init; } = CrackableHitOn.Use;

    /// <summary>
    /// The avatar effect a hitter has to wear (Sulake's <c>requiredEffectId</c>: the watering can,
    /// the magic wand); 0 for none.
    /// </summary>
    public int RequiredEffectId { get; init; }

    /// <summary>The achievement every hit counts towards (<c>incrementalHitAchievementName</c>).</summary>
    public string? IncrementalHitAchievement { get; init; }

    /// <summary>The achievement the cracking hit counts towards (<c>finalHitAchievementName</c>).</summary>
    public string? FinalHitAchievement { get; init; }

    /// <summary>How much the cracking hit counts (<c>finalHitAchievementCount</c>); 1 when not given.</summary>
    public int FinalHitAchievementCount { get; init; } = 1;

    /// <summary>What it may hold; one is drawn by weight.</summary>
    public ImmutableArray<CrackableReward> Rewards { get; init; } = [];

    /// <summary>Who gets what is inside.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CrackableRecipient RewardTo { get; init; } = CrackableRecipient.Owner;

    /// <summary>Where a furni reward goes.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CrackablePlacement RewardPlacement { get; init; } = CrackablePlacement.Room;

    public bool HasReward => Rewards.Any(x => x.IsValid);
}

/// <summary>
/// One thing a crackable may hold, drawn with this weight: a furni by definition name, credits,
/// or days of a membership (the Habbo Club and Builders Club boxes hold only that; a bonus bag
/// holds one of its rares, 5 credits or 3 days of Habbo Club).
/// </summary>
public sealed record CrackableReward
{
    public string? Furni { get; init; }

    public int Credits { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubscriptionType? Subscription { get; init; }

    public int SubscriptionDays { get; init; }

    public int Weight { get; init; } = 1;

    public bool IsValid =>
        Weight > 0
        && (
            !string.IsNullOrWhiteSpace(Furni)
            || Credits > 0
            || (Subscription is not null && SubscriptionDays > 0)
        );
}

/// <summary>What lands a hit on a crackable.</summary>
public enum CrackableHitOn
{
    /// <summary>Using it from beside it: a double-click, or the infostand's Use button.</summary>
    Use,

    /// <summary>
    /// Walking onto it (<c>PinataFurniture</c>: "use the Rainbow Piñata Stick effect and walk
    /// underneath it 100 times", the hotel's <c>catalog.page.pinatas.text_0</c>).
    /// </summary>
    Walk,
}

/// <summary>Who gets what a crackable holds.</summary>
public enum CrackableRecipient
{
    /// <summary>The furni's owner, whoever cracked it.</summary>
    Owner,

    /// <summary>Whoever landed the cracking hit (Sulake's <c>Public</c> crackables).</summary>
    Cracker,
}

/// <summary>Where a furni reward goes.</summary>
public enum CrackablePlacement
{
    /// <summary>On the tile the crackable stood on, or the inventory when it does not fit there.</summary>
    Room,

    /// <summary>Straight into the inventory (Sulake's <c>CrackableInventoryProductRewardFurniture</c>).</summary>
    Inventory,
}
