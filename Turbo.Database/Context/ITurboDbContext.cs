using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;
using Turbo.Database.Entities.Tracking;

namespace Turbo.Database.Context;

public interface ITurboDbContext : IDisposable
{
    public DbSet<CatalogOfferEntity> CatalogOffers { get; set; }

    public DbSet<CatalogPageEntity> CatalogPages { get; set; }

    public DbSet<CatalogProductEntity> CatalogProducts { get; set; }

    public DbSet<FurnitureDefinitionEntity> FurnitureDefinitions { get; set; }

    public DbSet<FurnitureEntity> Furnitures { get; set; }

    public DbSet<FurnitureTeleportLinkEntity> FurnitureTeleportLinks { get; set; }

    public DbSet<PlayerBadgeEntity> PlayerBadges { get; set; }

    public DbSet<PlayerCurrencyEntity> PlayerCurrencies { get; set; }

    public DbSet<PlayerEntity> Players { get; set; }

    public DbSet<RoomBanEntity> RoomBans { get; set; }

    public DbSet<RoomChatlogEntity> Chatlogs { get; set; }

    public DbSet<RoomEntity> Rooms { get; set; }

    public DbSet<RoomModelEntity> RoomModels { get; set; }

    public DbSet<RoomMuteEntity> RoomMutes { get; set; }

    public DbSet<RoomRightEntity> RoomRights { get; set; }

    public DbSet<RoomEntryLogEntity> RoomEntryLogs { get; set; }

    public DbSet<SecurityTicketEntity> SecurityTickets { get; set; }

    public DbSet<NavigatorTopLevelContextEntity> NavigatorTopLevelContexts { get; set; }

    public DbSet<NavigatorFlatCategoryEntity> NavigatorFlatCategories { get; set; }

    public DbSet<NavigatorEventCategoryEntity> NavigatorEventCategories { get; set; }

    public DbSet<PlayerChatStyleEntity> PlayerChatStyles { get; set; }

    public DbSet<PlayerChatStyleOwnedEntity> PlayerOwnedChatStyles { get; set; }

    public DbSet<PerformanceLogEntity> PerformanceLogs { get; set; }

    public DbSet<PlayerFavoriteRoomsEntity> PlayerFavouriteRooms { get; set; }

    public int SaveChanges(bool acceptAllChangesOnSuccess);

    public int SaveChanges();

    public Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    );

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public EntityEntry Add([NotNull] object entity);

    public EntityEntry<TEntity> Add<TEntity>([NotNull] TEntity entity)
        where TEntity : class;

    public EntityEntry Update([NotNull] object entity);

    public EntityEntry<TEntity> Update<TEntity>([NotNull] TEntity entity)
        where TEntity : class;

    public EntityEntry Remove([NotNull] object entity);

    public EntityEntry<TEntity> Remove<TEntity>([NotNull] TEntity entity)
        where TEntity : class;
}
