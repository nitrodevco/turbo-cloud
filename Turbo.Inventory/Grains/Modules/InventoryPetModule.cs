using System;
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
/// The pets a player keeps in the inventory. The hand-over flow is
/// <see cref="InventoryUnitModule{TEntity, TSnapshot}"/>; a pet brings its stats and figure
/// back from a room, and is sold as a product the buyer names.
/// </summary>
internal sealed class InventoryPetModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IPetBreedProvider petBreedProvider,
    ILogger logger
)
    : InventoryUnitModule<PetEntity, PetSnapshot>(
        inventoryGrain,
        liveState.Pets,
        dbCtxFactory,
        logger
    )
{
    private readonly IPetBreedProvider _petBreedProvider = petBreedProvider;

    protected override string Kind => "pet";

    protected override int MaxOwned => _inventoryGrain._inventoryConfig.MaxPets;

    protected override DbSet<PetEntity> Table(TurboDbContext dbCtx) => dbCtx.Pets;

    protected override PetSnapshot ToSnapshot(PetEntity entity, string ownerName) =>
        entity.ToSnapshot(ownerName);

    protected override PetSnapshot WithRoom(PetSnapshot snapshot, RoomId? roomId) =>
        snapshot with
        {
            RoomId = roomId,
        };

    protected override Task<int> WriteReturnedAsync(
        IQueryable<PetEntity> row,
        PetSnapshot snapshot,
        CancellationToken ct
    ) =>
        row.ExecuteUpdateAsync(
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
                    .SetProperty(p => p.HasBreedingPermission, snapshot.HasBreedingPermission)
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

    protected override Task OnAddedAsync(
        PetSnapshot snapshot,
        bool openInventory,
        CancellationToken ct
    ) => _inventoryGrain.Presence.OnPetAddedAsync(snapshot, openInventory, ct);

    protected override Task OnRemovedAsync(int id, CancellationToken ct) =>
        _inventoryGrain.Presence.OnPetRemovedAsync(id, ct);

    public Task<PetSnapshot?> CreateAsync(
        string name,
        int typeId,
        int paletteId,
        int breedId,
        string color,
        int rarityLevel,
        CancellationToken ct
    )
    {
        var config = _inventoryGrain._inventoryConfig;

        return CreateAsync(
            new PetEntity
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
            },
            ct
        );
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
