using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Wardrobe;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Database.Extensions;

/// <summary>Rows a player owns (wardrobe, wallet) and the currency types the wallet is keyed by.</summary>
public static class PlayerEntityExtensions
{
    public static OutfitDataSnapshot ToSnapshot(this PlayerOutfitEntity entity) =>
        new()
        {
            SlotId = entity.SlotId,
            Figure = entity.Figure,
            Gender = entity.Gender,
        };

    /// <param name="kind">The currency the row's type id stands for, resolved by the caller.</param>
    public static WalletCurrencySnapshot ToSnapshot(
        this PlayerCurrencyEntity entity,
        CurrencyKind kind
    ) =>
        new()
        {
            Id = entity.Id,
            CurrencyKind = kind,
            Amount = entity.Amount,
        };

    public static CurrencyTypeSnapshot ToSnapshot(this CurrencyTypeEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            CurrencyType = entity.CurrencyType,
            ActivityPointType = entity.ActivityPointType,
            Enabled = entity.Enabled,
        };
}
