using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerGrain
{
    public async Task<bool> SpendRespectOperationAsync(string operationId, CancellationToken ct) =>
        await SpendRespectQuotaAsync(operationId, false, ct);

    public async Task<bool> SpendPetRespectOperationAsync(
        string operationId,
        CancellationToken ct
    ) => await SpendRespectQuotaAsync(operationId, true, ct);

    private async Task<bool> SpendRespectQuotaAsync(
        string operationId,
        bool pet,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 100)
            throw new ArgumentException("Respect operation id too long.", nameof(operationId));
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var kind = pet ? "pet-spend" : "spend";
        var receipt = await db.HumanRespectParticipantReceipts.FindAsync(
            [PlayerId.Value, operationId, kind],
            ct
        );
        if (receipt is not null)
            return receipt.Accepted;
        var entity = await db.Players.SingleAsync(x => x.Id == PlayerId.Value, ct);
        var today = DateTime.UtcNow.Date;
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
        db.HumanRespectParticipantReceipts.Add(
            new()
            {
                PlayerId = PlayerId.Value,
                OperationId = operationId,
                Kind = kind,
                Accepted = accepted,
                ResultTotal = pet ? entity.PetRespectsLeft : entity.RespectsLeft,
            }
        );
        await db.SaveChangesAsync(ct);
        _state.RespectResetDate = entity.RespectResetDate;
        _state.RespectsLeft = entity.RespectsLeft;
        _state.PetRespectsLeft = entity.PetRespectsLeft;
        _state.RespectReplenishesLeft = entity.RespectReplenishesLeft;
        return accepted;
    }

    public async Task<int> ReceiveRespectOperationAsync(string operationId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 100)
            throw new ArgumentException("Respect operation id too long.", nameof(operationId));
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var receipt = await db.HumanRespectParticipantReceipts.FindAsync(
            [PlayerId.Value, operationId, "receive"],
            ct
        );
        if (receipt is not null)
            return receipt.ResultTotal;
        var entity = await db.Players.SingleAsync(x => x.Id == PlayerId.Value, ct);
        entity.RespectPoints = checked(entity.RespectPoints + 1);
        db.HumanRespectParticipantReceipts.Add(
            new()
            {
                PlayerId = PlayerId.Value,
                OperationId = operationId,
                Kind = "receive",
                Accepted = true,
                ResultTotal = entity.RespectPoints,
            }
        );
        await db.SaveChangesAsync(ct);
        _state.RespectPoints = entity.RespectPoints;
        return entity.RespectPoints;
    }
}
