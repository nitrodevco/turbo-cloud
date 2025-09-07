using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;
using Turbo.Database.Entities.Tracking;
using Turbo.Database.Extensions;

namespace Turbo.Database.Context;

public class TurboDbContext(
    DbContextOptions<TurboDbContext> options,
    IEnumerable<Assembly> pluginAssemblies
) : DbContext(options), ITurboDbContext
{
    private readonly IEnumerable<Assembly> _pluginAssemblies = pluginAssemblies;

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

    public DbSet<RoomEntity> Rooms { get; set; }

    public DbSet<RoomModelEntity> RoomModels { get; set; }

    public DbSet<RoomMuteEntity> RoomMutes { get; set; }

    public DbSet<RoomRightEntity> RoomRights { get; set; }

    public DbSet<RoomEntryLogEntity> RoomEntryLogs { get; set; }

    public DbSet<RoomChatlogEntity> Chatlogs { get; set; }

    public DbSet<SecurityTicketEntity> SecurityTickets { get; set; }

    public DbSet<NavigatorTopLevelContextEntity> NavigatorTopLevelContexts { get; set; }

    public DbSet<NavigatorFlatCategoryEntity> NavigatorFlatCategories { get; set; }

    public DbSet<NavigatorEventCategoryEntity> NavigatorEventCategories { get; set; }

    public DbSet<PlayerChatStyleEntity> PlayerChatStyles { get; set; }

    public DbSet<PlayerChatStyleOwnedEntity> PlayerOwnedChatStyles { get; set; }

    public DbSet<PerformanceLogEntity> PerformanceLogs { get; set; }

    public DbSet<PlayerFavoriteRoomsEntity> PlayerFavouriteRooms { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var asm in _pluginAssemblies.Distinct())
            modelBuilder.ApplyConfigurationsFromAssembly(asm);

        modelBuilder.ApplyDefaultAttributesFromEntities();

        /* var entityMethod = typeof(ModelBuilder).GetMethod("Entity", Type.EmptyTypes);

        if (!Directory.Exists("plugins"))
        {
            Directory.CreateDirectory("plugins");
        }

        var plugins = Directory.GetFiles("plugins", "*.dll");

        foreach (var plugin in plugins)
        {
            // Load assembly
            var assembly = Assembly.LoadFile(Path.Combine(Directory.GetCurrentDirectory(), plugin));

            var entityTypes = assembly
                .GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(TurboEntity), true).Any());

            foreach (var type in entityTypes)
            {
                entityMethod.MakeGenericMethod(type).Invoke(modelBuilder, new object[] { });
            }
        } */
    }
}
