using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerWalletGrain
{
    /// <summary>
    /// The balance and the receipt of the reference are one SaveChanges, so either both exist or
    /// neither does. The primary key of the receipt settles a race with another activation.
    /// </summary>
    public async Task<WalletCreditResult> CreditAsync(
        CurrencyKind kind,
        int amount,
        string reference,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            reference.Length,
            WalletCreditReference.MaxLength
        );

        if (amount <= 0)
            return WalletCreditResult.Rejected;

        if (await ReceiptExistsAsync(reference, ct))
            return WalletCreditResult.AlreadyApplied;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        PlayerCurrencyEntity? entity;

        if (_state.CurrenciesByKind.TryGetValue(kind, out var snapshot))
        {
            entity = await dbCtx.PlayerCurrencies.FirstOrDefaultAsync(
                x => x.Id == snapshot.Id && x.PlayerEntityId == PlayerId.Value,
                ct
            );
        }
        else if (_currencyTypeProvider.TryGetCurrencyTypeId(kind, out var typeId))
        {
            entity = new PlayerCurrencyEntity
            {
                PlayerEntityId = PlayerId.Value,
                CurrencyTypeEntityId = typeId,
                Amount = 0,
            };
            dbCtx.PlayerCurrencies.Add(entity);
        }
        else
            entity = null;

        if (entity is null || entity.Amount > int.MaxValue - amount)
            return WalletCreditResult.Rejected;

        entity.Amount += amount;
        dbCtx.WalletCreditReceipts.Add(
            new WalletCreditReceiptEntity { PlayerId = PlayerId.Value, Reference = reference }
        );

        try
        {
            await dbCtx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (!await ReceiptExistsAsync(reference, ct))
                throw;

            // Another activation applied it first and our whole save rolled back.
            await HydrateAsync(ct);

            return WalletCreditResult.AlreadyApplied;
        }

        _state.CurrenciesByKind[kind] = entity.ToSnapshot(kind);

        var balance = ToBalanceComposer(
            new WalletCurrencyUpdateSnapshot
            {
                CurrencyKind = kind,
                ChangedBy = amount,
                Amount = entity.Amount,
            }
        );

        if (balance is not null)
            await _grainFactory.TrySendComposerToPlayerAsync(PlayerId, balance, ct);

        return WalletCreditResult.Applied;
    }

    private async Task<bool> ReceiptExistsAsync(string reference, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        return await dbCtx.WalletCreditReceipts.AnyAsync(
            x => x.PlayerId == PlayerId.Value && x.Reference == reference,
            ct
        );
    }
}
