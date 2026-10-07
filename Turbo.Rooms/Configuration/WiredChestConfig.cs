namespace Turbo.Rooms.Configuration;

/// <summary>
/// Wired chests: how much they hold, what buying more costs, and the bounds of the windows that
/// list them. The capacities and costs ship as the client's own defaults (its config keys
/// <c>wired.furni_chest.*</c>, <c>wired.coins_chest.*</c>, <c>wired.chests.*</c>); a hotel that
/// changes one changes the other, or the client shows numbers the server does not keep.
/// </summary>
public class WiredChestConfig
{
    public const string SECTION_NAME = "Turbo:WiredChests";

    /// <summary>What a furni chest holds before any upgrade, in items.</summary>
    public int FurniInitialCapacity { get; init; } = 1000;

    /// <summary>Items each capacity upgrade adds to a furni chest.</summary>
    public int FurniUpgradeCapacity { get; init; } = 1000;

    public int FurniMaxUpgrades { get; init; } = 9;

    /// <summary>What a starter furni chest holds; a starter chest is never upgraded.</summary>
    public int FurniStarterCapacity { get; init; } = 100;

    /// <summary>What a credit chest holds before any upgrade, in credits.</summary>
    public int CoinsInitialCapacity { get; init; } = 5000;

    public int CoinsUpgradeCapacity { get; init; } = 5000;

    public int CoinsMaxUpgrades { get; init; } = 19;

    public int CoinsStarterCapacity { get; init; } = 500;

    /// <summary>The part of a chest's furni name that marks it a starter chest.</summary>
    public string StarterInfix { get; init; } = "_starter";

    /// <summary>Credits one capacity upgrade costs.</summary>
    public int UpgradeCostCredits { get; init; } = 10;

    /// <summary>Diamonds one capacity upgrade costs.</summary>
    public int UpgradeCostDiamonds { get; init; } = 10;

    /// <summary>The activity point type diamonds are kept under.</summary>
    public int DiamondsActivityPointType { get; init; } = 5;

    public int NameMaxLength { get; init; } = 30;
    public int DescriptionMaxLength { get; init; } = 200;

    /// <summary>The most items a chest can show above itself when open.</summary>
    public int MaxPreviewItems { get; init; } = 4;

    /// <summary>Items per contents packet when a furni chest is opened.</summary>
    public int ContentsFragmentSize { get; init; } = 500;

    /// <summary>Players one chest keeps up to date while they look inside; more are refused.</summary>
    public int MaxViewersPerChest { get; init; } = 50;

    /// <summary>Items one deposit may offer.</summary>
    public int MaxItemsPerDeposit { get; init; } = 500;

    /// <summary>The most transactions one page of the logs window lists.</summary>
    public int MaxLogPageSize { get; init; } = 50;

    /// <summary>Item types one log details window lists before it says there are more.</summary>
    public int MaxLogDetailItemTypes { get; init; } = 100;

    // A contract, as its editor bounds it.

    /// <summary>Payment options a contract may offer.</summary>
    public int MaxContractRules { get; init; } = 3;

    /// <summary>Elements in one option, or in what a contract gives.</summary>
    public int MaxContractNodes { get; init; } = 5;

    public int MaxContractCoins { get; init; } = 100000;
    public int MaxContractFurni { get; init; } = 500;
    public int ContractReceiveTextMaxLength { get; init; } = 60;
    public int ContractRewardTextMaxLength { get; init; } = 200;

    /// <summary>The most times one transaction may repeat its contract.</summary>
    public int MaxTransactionMultiplier { get; init; } = 500;

    /// <summary>
    /// How long a trade wired cancelled stays open in case the same stack opens a new one, which
    /// then replaces it without the window closing.
    /// </summary>
    public int CancelGraceMs { get; init; } = 500;
}
