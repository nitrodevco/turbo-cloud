using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Messenger;
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

    public DbSet<CurrencyTypeEntity> CurrencyTypes { get; init; }

    public DbSet<CatalogPageEntity> CatalogPages { get; init; }

    public DbSet<CatalogProductEntity> CatalogProducts { get; init; }
    public DbSet<FurnitureDefinitionEntity> FurnitureDefinitions { get; init; }

    public DbSet<FurnitureEntity> Furnitures { get; init; }

    public DbSet<FurnitureTeleportLinkEntity> FurnitureTeleportLinks { get; init; }

    public DbSet<PlayerBadgeEntity> PlayerBadges { get; init; }

    public DbSet<PlayerCurrencyEntity> PlayerCurrencies { get; init; }
    public DbSet<PlayerEntity> Players { get; init; }
    public DbSet<PlayerRespectEntity> PlayerRespects { get; init; }

    public DbSet<RoomBanEntity> RoomBans { get; init; }

    public DbSet<RoomEntity> Rooms { get; init; }

    public DbSet<RoomModelEntity> RoomModels { get; init; }

    public DbSet<RoomMuteEntity> RoomMutes { get; init; }

    public DbSet<RoomRightEntity> RoomRights { get; init; }

    public DbSet<RoomEntryLogEntity> RoomEntryLogs { get; init; }

    public DbSet<RoomChatlogEntity> Chatlogs { get; init; }
    public DbSet<SecurityTicketEntity> SecurityTickets { get; init; }

    public DbSet<NavigatorTopLevelContextEntity> NavigatorTopLevelContexts { get; init; }

    public DbSet<NavigatorFlatCategoryEntity> NavigatorFlatCategories { get; init; }

    public DbSet<NavigatorEventCategoryEntity> NavigatorEventCategories { get; init; }

    public DbSet<PlayerChatStyleEntity> PlayerChatStyles { get; init; }
    public DbSet<PlayerChatStyleOwnedEntity> PlayerOwnedChatStyles { get; init; }

    public DbSet<PerformanceLogEntity> PerformanceLogs { get; init; }

    public DbSet<PlayerFavoriteRoomsEntity> PlayerFavouriteRooms { get; init; }

    public DbSet<LtdSeriesEntity> LtdSeries { get; init; }

    public DbSet<LtdRaffleEntryEntity> LtdRaffleEntries { get; init; }

    public DbSet<MessengerFriendEntity> MessengerFriends { get; init; }

    public DbSet<MessengerRequestEntity> MessengerRequests { get; init; }

    public DbSet<MessengerCategoryEntity> MessengerCategories { get; init; }

    public DbSet<MessengerMessageEntity> MessengerMessages { get; init; }

    public DbSet<MessengerBlockedEntity> MessengerBlocked { get; init; }

    public DbSet<MessengerIgnoredEntity> MessengerIgnored { get; init; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
    }
}
