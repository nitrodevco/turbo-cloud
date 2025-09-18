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
    : DbContextBase<TurboDbContext>(options),
        ITurboDbContext
{
    public DbSet<CatalogOfferEntity>? CatalogOffers { get; set; }

    public DbSet<CatalogPageEntity>? CatalogPages { get; set; }

    public DbSet<CatalogProductEntity>? CatalogProducts { get; set; }

    public DbSet<FurnitureDefinitionEntity>? FurnitureDefinitions { get; set; }

    public DbSet<FurnitureEntity>? Furnitures { get; set; }

    public DbSet<FurnitureTeleportLinkEntity>? FurnitureTeleportLinks { get; set; }

    public DbSet<PlayerBadgeEntity>? PlayerBadges { get; set; }

    public DbSet<PlayerCurrencyEntity>? PlayerCurrencies { get; set; }

    public DbSet<PlayerEntity>? Players { get; set; }

    public DbSet<RoomBanEntity>? RoomBans { get; set; }

    public DbSet<RoomEntity>? Rooms { get; set; }

    public DbSet<RoomModelEntity>? RoomModels { get; set; }

    public DbSet<RoomMuteEntity>? RoomMutes { get; set; }

    public DbSet<RoomRightEntity>? RoomRights { get; set; }

    public DbSet<RoomEntryLogEntity>? RoomEntryLogs { get; set; }

    public DbSet<RoomChatlogEntity>? Chatlogs { get; set; }

    public DbSet<SecurityTicketEntity>? SecurityTickets { get; set; }

    public DbSet<NavigatorTopLevelContextEntity>? NavigatorTopLevelContexts { get; set; }

    public DbSet<NavigatorFlatCategoryEntity>? NavigatorFlatCategories { get; set; }

    public DbSet<NavigatorEventCategoryEntity>? NavigatorEventCategories { get; set; }

    public DbSet<PlayerChatStyleEntity>? PlayerChatStyles { get; set; }

    public DbSet<PlayerChatStyleOwnedEntity>? PlayerOwnedChatStyles { get; set; }

    public DbSet<PerformanceLogEntity>? PerformanceLogs { get; set; }

    public DbSet<PlayerFavoriteRoomsEntity>? PlayerFavouriteRooms { get; set; }
}
