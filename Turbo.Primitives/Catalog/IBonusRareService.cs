using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// The bonus rare: campaigns that give a furniture for every so many credits a player brings in,
/// counted as the running campaign says (<see cref="BonusRareSource"/>). Reaching the target gives
/// the reward once, however many silos count at the same time.
/// </summary>
public interface IBonusRareService
{
    /// <summary>The running campaign as the player's widget shows it; hidden when none runs.</summary>
    public Task<BonusRareSnapshot> GetInfoAsync(PlayerId player, CancellationToken ct);

    /// <summary>
    /// Credits the player spent in the normal catalogue; they count when the running campaign
    /// counts catalogue spending.
    /// </summary>
    public Task RecordCatalogSpendingAsync(PlayerId player, int credits, CancellationToken ct);

    /// <summary>
    /// Credits the player bought, from the hotel's purchase integration, under its own receipt;
    /// they count when the running campaign counts bought credits. A receipt recorded before
    /// counts nothing again.
    /// </summary>
    public Task<BonusRarePurchaseResult> RecordPurchaseAsync(
        PlayerId player,
        int credits,
        string reference,
        CancellationToken ct
    );

    /// <summary>Every campaign, the latest start first.</summary>
    public Task<ImmutableArray<BonusRareCampaignSnapshot>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Adds a campaign (id 0) or changes one. Throws <see cref="System.ArgumentException"/> for a
    /// code that is empty or taken, furniture the hotel has no definition of, no credits required,
    /// or an end before the start.
    /// </summary>
    public Task<BonusRareCampaignSnapshot> SaveAsync(
        BonusRareCampaignSnapshot campaign,
        CancellationToken ct
    );

    /// <summary>
    /// Removes a campaign; its players' progress stays under its code. False when there is none.
    /// </summary>
    public Task<bool> DeleteAsync(int id, CancellationToken ct);

    /// <summary>How a campaign has gone, by its code.</summary>
    public Task<BonusRareStandingSnapshot> GetStandingAsync(string code, CancellationToken ct);
}
