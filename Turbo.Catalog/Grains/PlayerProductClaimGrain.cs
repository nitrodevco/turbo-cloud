using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Catalog.Grains;

/// <summary>
/// A player's free product claims, write-through: a claim is saved before what it gives is handed
/// out, so a second click (or a second client) finds it taken. The claims are configuration
/// (<see cref="ProductClaimConfig"/>); the grain keeps a row per claim the player has taken.
/// </summary>
internal sealed class PlayerProductClaimGrain : Grain, IPlayerProductClaimGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly ProductClaimConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly IPetBreedProvider _petBreeds;
    private readonly ILogger<IPlayerProductClaimGrain> _logger;

    private readonly PlayerProductClaimLiveState _state;

    public PlayerProductClaimGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<ProductClaimConfig> config,
        IGrainFactory grainFactory,
        IPetBreedProvider petBreeds,
        ILogger<IPlayerProductClaimGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _petBreeds = petBreeds;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var claimed = await dbCtx
            .PlayerProductClaims.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .Select(x => x.ClaimId)
            .ToListAsync(ct);

        _state.Claimed.UnionWith(claimed);

        await base.OnActivateAsync(ct);
    }

    public Task<bool> HasClaimedAsync(string claimId, CancellationToken ct) =>
        Task.FromResult(_state.Claimed.Contains(claimId));

    public async Task<ProductClaimResult> ClaimAsync(
        string claimId,
        int choice,
        CancellationToken ct
    )
    {
        if (!_config.Claims.TryGetValue(claimId, out var claim))
            return ProductClaimResult.NotFound;

        if (!claim.Active)
            return ProductClaimResult.NotActive;

        if (_state.Claimed.Contains(claimId))
            return ProductClaimResult.AlreadyClaimed;

        // ClaimProductMessageComposer leaves the index out when it is -1: only a claim with a
        // single choice can be taken without one.
        if (choice == -1 && claim.Choices.Count == 1)
            choice = 0;

        if (choice < 0 || choice >= claim.Choices.Count)
            return ProductClaimResult.Failed;

        var given = claim.Choices[choice];
        var pet = PetOf(given.Pet);

        // Checked before the claim is written: a hotel without the pet's palette gives nothing and
        // leaves the claim open, rather than spending it on a pet that cannot be made.
        if (pet is null)
        {
            _logger.LogWarning(
                "Product claim {ClaimId} choice {Choice} names a pet the hotel has no palette for: {Pet}",
                claimId,
                choice,
                given.Pet
            );

            return ProductClaimResult.Failed;
        }

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.PlayerProductClaims.Add(
                new PlayerProductClaimEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    ClaimId = claimId,
                    Choice = choice,
                }
            );

            await dbCtx.SaveChangesAsync(ct);
        }

        _state.Claimed.Add(claimId);

        await _grainFactory
            .GetInventoryGrain(_state.PlayerId)
            .CreatePetAsync(
                given.PetName.Length > 0 ? given.PetName : ProductClaimChoice.DefaultPetName,
                pet.Value.Palette.TypeId,
                pet.Value.Palette.PaletteId,
                pet.Value.Palette.BreedId,
                pet.Value.Color,
                pet.Value.Palette.RarityLevel,
                [],
                ct
            );

        _logger.LogInformation(
            "Player {PlayerId} claimed {ClaimId} with choice {Choice}",
            _state.PlayerId,
            claimId,
            choice
        );

        return ProductClaimResult.Claimed;
    }

    /// <summary>The hotel's palette and the colour of a "type palette colour" pet figure, or null.</summary>
    private (PetBreedSnapshot Palette, string Color)? PetOf(string figure)
    {
        var parts = figure.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (
            parts.Length < 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var type)
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var palette
            )
            || _petBreeds.TryGetPalette(type, palette) is not { } breed
        )
            return null;

        return (breed, parts[2]);
    }
}
