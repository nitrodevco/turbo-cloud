using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Wardrobe;
using Turbo.Primitives.Players.Snapshots.Wardrobe;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Players.Grains.Wardrobe;

/// <summary>
/// Owns a player's saved outfits. Each save writes through to the database before the in-memory
/// slot is updated, so there is nothing to flush on deactivation.
/// </summary>
internal sealed class PlayerWardrobeGrain : Grain, IPlayerWardrobeGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PlayerConfig _playerConfig;
    private readonly ILogger<IPlayerWardrobeGrain> _logger;

    private readonly PlayerWardrobeLiveState _state;

    public PlayerWardrobeGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        ILogger<IPlayerWardrobeGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _playerConfig = playerConfig.Value;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate wardrobe for player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }
    }

    public Task<List<OutfitDataSnapshot>> GetOutfitsAsync(CancellationToken ct) =>
        Task.FromResult(_state.OutfitsBySlot.Values.ToList());

    public async Task<bool> SaveOutfitAsync(
        int slotId,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    )
    {
        if (slotId < 1 || slotId > _playerConfig.WardrobeMaxSlots)
        {
            _logger.LogWarning(
                "Rejected wardrobe slot {SlotId} for player {PlayerId}: outside 1..{MaxSlots}",
                slotId,
                _state.PlayerId,
                _playerConfig.WardrobeMaxSlots
            );

            return false;
        }

        if (
            string.IsNullOrWhiteSpace(figure)
            || figure.Length > PlayerOutfitEntity.FIGURE_MAX_LENGTH
        )
        {
            _logger.LogWarning(
                "Rejected wardrobe figure for player {PlayerId} slot {SlotId}: empty or longer than {MaxLength}",
                _state.PlayerId,
                slotId,
                PlayerOutfitEntity.FIGURE_MAX_LENGTH
            );

            return false;
        }

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await dbCtx.PlayerOutfits.FirstOrDefaultAsync(
            x => x.PlayerEntityId == _state.PlayerId.Value && x.SlotId == slotId,
            ct
        );

        if (entity is null)
        {
            entity = new PlayerOutfitEntity
            {
                PlayerEntityId = _state.PlayerId.Value,
                SlotId = slotId,
                Figure = figure,
                Gender = gender,
            };

            dbCtx.PlayerOutfits.Add(entity);
        }
        else
        {
            entity.Figure = figure;
            entity.Gender = gender;
        }

        await dbCtx.SaveChangesAsync(ct);

        _state.OutfitsBySlot[slotId] = entity.ToSnapshot();

        return true;
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        _state.OutfitsBySlot.Clear();

        var maxSlots = _playerConfig.WardrobeMaxSlots;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .PlayerOutfits.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == _state.PlayerId.Value && x.SlotId >= 1 && x.SlotId <= maxSlots
            )
            .ToListAsync(ct);

        foreach (var entity in entities)
        {
            _state.OutfitsBySlot[entity.SlotId] = entity.ToSnapshot();
        }
    }
}
