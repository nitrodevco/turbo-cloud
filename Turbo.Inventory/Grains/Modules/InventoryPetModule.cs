using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Extensions;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Inventory.Grains.Modules;

/// <summary>
/// The pets a player keeps in the inventory (not standing in a room). Loaded on first use like
/// furniture; every hand-over to or from a room writes the row before the list changes, so a
/// crash in between leaves the pet where the database says it is.
/// </summary>
internal sealed class InventoryPetModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IPetBreedProvider petBreedProvider,
    ILogger logger
)
{
    private readonly InventoryGrain _inventoryGrain = inventoryGrain;
    private readonly InventoryLiveState _state = liveState;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IPetBreedProvider _petBreedProvider = petBreedProvider;
    private readonly ILogger _logger = logger;

    private int OwnerId => (int)_inventoryGrain.PlayerId;

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_state.IsPetsReady)
            return;

        var ownerName = await _inventoryGrain.GetOwnerNameAsync(ct);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .Pets.AsNoTracking()
            .Where(x => x.PlayerEntityId == OwnerId && x.RoomEntityId == null)
            .ToListAsync(ct);

        _state.PetsById.Clear();

        foreach (var entity in entities)
            _state.PetsById[entity.Id] = entity.ToSnapshot(ownerName);

        _state.IsPetsReady = true;
    }

    public async Task<ImmutableArray<PetSnapshot>> GetAllAsync(CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return [.. _state.PetsById.Values];
    }

    public async Task<PetSnapshot?> GetAsync(int petId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return _state.PetsById.TryGetValue(petId, out var pet) ? pet : null;
    }

    /// <summary>Hands a pet to a room. Null when it is not here to give.</summary>
    public async Task<PetSnapshot?> TryCheckOutAsync(int petId, RoomId roomId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        if (!_state.PetsById.TryGetValue(petId, out var pet))
            return null;

        int updated;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            updated = await dbCtx
                .Pets.Where(x =>
                    x.Id == petId && x.PlayerEntityId == OwnerId && x.RoomEntityId == null
                )
                .ExecuteUpdateAsync(up => up.SetProperty(p => p.RoomEntityId, roomId.Value), ct);
        }

        if (updated == 0)
        {
            _logger.LogWarning(
                "Pet {PetId} of player {PlayerId} is listed in the inventory but its row is elsewhere; reloading",
                petId,
                _inventoryGrain.PlayerId
            );

            _state.IsPetsReady = false;

            return null;
        }

        _state.PetsById.Remove(petId);

        await _inventoryGrain.Presence.OnPetRemovedAsync(petId, ct);

        return pet with
        {
            RoomId = roomId,
        };
    }

    /// <summary>Takes a pet back from a room, with the stats it earned there.</summary>
    public async Task<bool> ReturnAsync(PetSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot.OwnerId != _inventoryGrain.PlayerId)
            return false;

        await EnsureReadyAsync(ct);

        int updated;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            updated = await dbCtx
                .Pets.Where(x => x.Id == snapshot.Id && x.PlayerEntityId == OwnerId)
                .ExecuteUpdateAsync(
                    up =>
                        up.SetProperty(p => p.RoomEntityId, (int?)null)
                            .SetProperty(p => p.Name, snapshot.Name)
                            .SetProperty(p => p.Level, snapshot.Level)
                            .SetProperty(p => p.Experience, snapshot.Experience)
                            .SetProperty(p => p.Energy, snapshot.Energy)
                            .SetProperty(p => p.Nutrition, snapshot.Nutrition)
                            .SetProperty(p => p.Respect, snapshot.Respect)
                            .SetProperty(p => p.HasSaddle, snapshot.HasSaddle)
                            .SetProperty(p => p.AnyoneCanRide, snapshot.AnyoneCanRide)
                            .SetProperty(
                                p => p.HasBreedingPermission,
                                snapshot.HasBreedingPermission
                            )
                            .SetProperty(p => p.PaletteId, snapshot.Figure.PaletteId)
                            .SetProperty(p => p.Color, snapshot.Figure.Color)
                            .SetProperty(
                                p => p.CustomParts,
                                PetFigure.SerializeCustomParts(snapshot.Figure.CustomParts)
                            )
                            .SetProperty(p => p.WateredAt, snapshot.WateredAtUtc)
                            .SetProperty(p => p.HarvestedAt, snapshot.HarvestedAtUtc),
                    ct
                );
        }

        if (updated == 0)
        {
            _logger.LogError(
                "Pet {PetId} returned to player {PlayerId} has no row to update",
                snapshot.Id,
                _inventoryGrain.PlayerId
            );

            return false;
        }

        var returned = snapshot with { RoomId = null };

        _state.PetsById[returned.Id] = returned;

        await _inventoryGrain.Presence.OnPetAddedAsync(returned, false, ct);

        return true;
    }

    /// <summary>
    /// Creates a pet in the inventory. Null when the player already owns the configured
    /// maximum, counting the pets standing in rooms.
    /// </summary>
    public async Task<PetSnapshot?> CreateAsync(
        string name,
        int typeId,
        int paletteId,
        int breedId,
        string color,
        int rarityLevel,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        var config = _inventoryGrain._inventoryConfig;
        var entity = new PetEntity
        {
            PlayerEntityId = OwnerId,
            Name = name,
            TypeId = typeId,
            PaletteId = paletteId,
            BreedId = breedId,
            Color = color,
            RarityLevel = rarityLevel,
            Energy = config.PetStartEnergy,
            Nutrition = config.PetStartNutrition,
            WateredAt = DateTime.UtcNow,
        };

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var owned = await dbCtx.Pets.CountAsync(x => x.PlayerEntityId == OwnerId, ct);

            if (owned >= config.MaxPets)
            {
                _logger.LogWarning(
                    "Player {PlayerId} owns {Count} pets, the configured maximum; not creating another",
                    _inventoryGrain.PlayerId,
                    owned
                );

                return null;
            }

            dbCtx.Add(entity);

            await dbCtx.SaveChangesAsync(ct);
        }

        var snapshot = entity.ToSnapshot(await _inventoryGrain.GetOwnerNameAsync(ct));

        _state.PetsById[snapshot.Id] = snapshot;

        await _inventoryGrain.Presence.OnPetAddedAsync(snapshot, true, ct);

        return snapshot;
    }

    public async Task<bool> DeleteAsync(int petId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        int deleted;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            deleted = await dbCtx
                .Pets.Where(x => x.Id == petId && x.PlayerEntityId == OwnerId)
                .ExecuteDeleteAsync(ct);
        }

        if (deleted == 0)
            return false;

        if (_state.PetsById.Remove(petId))
            await _inventoryGrain.Presence.OnPetRemovedAsync(petId, ct);

        return true;
    }

    /// <summary>
    /// Checks a pet product before anything is created. The product names its type through its
    /// class name (<c>pet&lt;typeId&gt;</c>) or a numeric extra param; the purchase's extra param
    /// carries the name, palette and colour the buyer chose.
    /// </summary>
    public PetProductGrant ValidateProduct(
        CatalogOfferSnapshot offer,
        CatalogProductSnapshot product,
        string extraParam
    )
    {
        if (
            !PetProductCodes.TryGetTypeId(product.ClassName, out var typeId)
            && !int.TryParse(product.ExtraParam, out typeId)
        )
        {
            _logger.LogError(
                "Pet product {ProductId} of offer {OfferId} names no pet type; cannot grant it to player {PlayerId}",
                product.Id,
                offer.Id,
                _inventoryGrain.PlayerId
            );

            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
        }

        if (!PetPurchaseData.TryParse(extraParam, out var purchase))
        {
            _logger.LogWarning(
                "Player {PlayerId} bought pet offer {OfferId} with an unreadable extra param; refusing",
                _inventoryGrain.PlayerId,
                offer.Id
            );

            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
        }

        var palette = _petBreedProvider.TryGetPalette(typeId, purchase.PaletteId);

        if (palette is null || !palette.Sellable)
        {
            _logger.LogWarning(
                "Player {PlayerId} bought pet type {TypeId} palette {PaletteId}, which is not sellable; refusing",
                _inventoryGrain.PlayerId,
                typeId,
                purchase.PaletteId
            );

            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
        }

        var config = _inventoryGrain._inventoryConfig;
        var nameStatus = PetNames.Validate(
            purchase.Name,
            config.PetNameMinLength,
            config.PetNameMaxLength
        );

        if (nameStatus != PetNameValidationType.Ok)
        {
            _logger.LogWarning(
                "Player {PlayerId} bought pet offer {OfferId} with a rejected name ({Status}); refusing",
                _inventoryGrain.PlayerId,
                offer.Id,
                nameStatus
            );

            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
        }

        return new PetProductGrant(
            purchase.Name,
            typeId,
            purchase.PaletteId,
            palette.BreedId,
            purchase.Color,
            palette.RarityLevel
        );
    }

    public async Task GrantProductAsync(PetProductGrant grant, CancellationToken ct)
    {
        var pet = await CreateAsync(
            grant.Name,
            grant.TypeId,
            grant.PaletteId,
            grant.BreedId,
            grant.Color,
            grant.RarityLevel,
            ct
        );

        if (pet is null)
            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
    }
}
