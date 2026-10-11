using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerGrain
{
    public async Task<bool> SpendRespectOperationAsync(Guid respectId, CancellationToken ct) =>
        await SpendRespectQuotaAsync(respectId, false, ct);

    public async Task<bool> SpendPetRespectOperationAsync(Guid respectId, CancellationToken ct) =>
        await SpendRespectQuotaAsync(respectId, true, ct);

    private async Task<bool> SpendRespectQuotaAsync(Guid respectId, bool pet, CancellationToken ct)
    {
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var kind = pet ? RespectChangeKind.PetSpent : RespectChangeKind.Spent;
        var change = await db.PlayerRespectChanges.FindAsync([respectId, kind], ct);
        if (change is not null)
            return change.Accepted;
        var entity = await db.Players.SingleAsync(x => x.Id == PlayerId.Value, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (entity.RespectResetDate != today)
        {
            entity.RespectResetDate = today;
            entity.RespectsLeft = _playerConfig.MaxRespectPerDay;
            entity.PetRespectsLeft = _playerConfig.MaxPetRespectPerDay;
            entity.RespectReplenishesLeft = _playerConfig.RespectReplenishesPerDay;
        }
        var accepted = (pet ? entity.PetRespectsLeft : entity.RespectsLeft) > 0;
        if (accepted)
        {
            if (pet)
                entity.PetRespectsLeft--;
            else
                entity.RespectsLeft--;
        }
        db.PlayerRespectChanges.Add(
            new()
            {
                RespectId = respectId,
                Kind = kind,
                PlayerId = PlayerId.Value,
                Accepted = accepted,
                TotalAfter = pet ? entity.PetRespectsLeft : entity.RespectsLeft,
            }
        );
        await db.SaveChangesAsync(ct);
        _state.RespectResetDate = entity.RespectResetDate;
        _state.RespectsLeft = entity.RespectsLeft;
        _state.PetRespectsLeft = entity.PetRespectsLeft;
        _state.RespectReplenishesLeft = entity.RespectReplenishesLeft;
        return accepted;
    }

    public async Task<int> ReceiveRespectOperationAsync(Guid respectId, CancellationToken ct)
    {
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var change = await db.PlayerRespectChanges.FindAsync(
            [respectId, RespectChangeKind.Received],
            ct
        );
        if (change is not null)
            return change.TotalAfter;
        var entity = await db.Players.SingleAsync(x => x.Id == PlayerId.Value, ct);
        entity.RespectPoints = checked(entity.RespectPoints + 1);
        db.PlayerRespectChanges.Add(
            new()
            {
                RespectId = respectId,
                Kind = RespectChangeKind.Received,
                PlayerId = PlayerId.Value,
                Accepted = true,
                TotalAfter = entity.RespectPoints,
            }
        );
        await db.SaveChangesAsync(ct);
        _state.RespectPoints = entity.RespectPoints;
        return entity.RespectPoints;
    }
}
