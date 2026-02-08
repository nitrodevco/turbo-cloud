using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;
using Turbo.Database.Entities.Tracking;

namespace Turbo.Database.Context;

public class TurboDbContext(DbContextOptions<TurboDbContext> options)
    : DbContextBase<TurboDbContext>(options)
{
    public DbSet<CatalogOfferEntity> CatalogOffers { get; init; }

    public required DbSet<CurrencyTypeEntity> CurrencyTypes { get; init; }

    public required DbSet<CatalogPageEntity> CatalogPages { get; init; }

    public required DbSet<CatalogProductEntity> CatalogProducts { get; init; }
    public required DbSet<FurnitureDefinitionEntity> FurnitureDefinitions { get; init; }

    public required DbSet<FurnitureEntity> Furnitures { get; init; }

    public required DbSet<FurnitureTeleportLinkEntity> FurnitureTeleportLinks { get; init; }

    public required DbSet<PlayerBadgeEntity> PlayerBadges { get; init; }

    public required DbSet<PlayerCurrencyEntity> PlayerCurrencies { get; init; }
    public required DbSet<PlayerEntity> Players { get; init; }

    public required DbSet<RoomBanEntity> RoomBans { get; init; }

    public required DbSet<RoomEntity> Rooms { get; init; }

    public required DbSet<RoomModelEntity> RoomModels { get; init; }

    public required DbSet<RoomMuteEntity> RoomMutes { get; init; }

    public required DbSet<RoomRightEntity> RoomRights { get; init; }

    public required DbSet<RoomEntryLogEntity> RoomEntryLogs { get; init; }

    public required DbSet<RoomChatlogEntity> Chatlogs { get; init; }
    public required DbSet<SecurityTicketEntity> SecurityTickets { get; init; }

    public required DbSet<NavigatorTopLevelContextEntity> NavigatorTopLevelContexts { get; init; }

    public required DbSet<NavigatorFlatCategoryEntity> NavigatorFlatCategories { get; init; }

    public required DbSet<NavigatorEventCategoryEntity> NavigatorEventCategories { get; init; }

    public required DbSet<PlayerChatStyleEntity> PlayerChatStyles { get; init; }
    public required DbSet<PlayerChatStyleOwnedEntity> PlayerOwnedChatStyles { get; init; }

    public required DbSet<PerformanceLogEntity> PerformanceLogs { get; init; }

    public required DbSet<PlayerFavoriteRoomsEntity> PlayerFavouriteRooms { get; init; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // currency_type ids are protocol identifiers (0, 5, 101...), not auto-generated keys
        mb.Entity<CurrencyTypeEntity>().Property(x => x.Id).ValueGeneratedNever();
    }
}
