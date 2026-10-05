using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Admin;
using Turbo.Database.Entities.Badges;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;
using Turbo.Database.Entities.Tracking;

namespace Turbo.Database.Context;

public class TurboDbContext(DbContextOptions<TurboDbContext> options)
    : DbContextBase<TurboDbContext>(options)
{
    public DbSet<AchievementDefinitionEntity> AchievementDefinitions { get; init; }
    public DbSet<AchievementFactEntity> AchievementFacts { get; init; }
    public DbSet<AchievementProgressEntity> AchievementProgress { get; init; }
    public DbSet<AchievementDistinctValueEntity> AchievementDistinctValues { get; init; }
    public DbSet<AchievementProjectionEntity> AchievementProjections { get; init; }
    public DbSet<AchievementAuditEntity> AchievementAudit { get; init; }
    public DbSet<AchievementWalletReceiptEntity> AchievementWalletReceipts { get; init; }
    public DbSet<HumanRespectOperationEntity> HumanRespectOperations { get; init; }
    public DbSet<HumanRespectParticipantReceiptEntity> HumanRespectParticipantReceipts { get; init; }
    public DbSet<PetNutritionOperationEntity> PetNutritionOperations { get; init; }
    public DbSet<PetRespectOperationEntity> PetRespectOperations { get; init; }

    public DbSet<CatalogOfferEntity> CatalogOffers { get; init; }

    public DbSet<CurrencyTypeEntity> CurrencyTypes { get; init; }

    public DbSet<CatalogPageEntity> CatalogPages { get; init; }

    public DbSet<CatalogProductEntity> CatalogProducts { get; init; }
    public DbSet<FurnitureDefinitionEntity> FurnitureDefinitions { get; init; }

    public DbSet<FurnitureEntity> Furnitures { get; init; }

    public DbSet<BuildersClubFurnitureEntity> BuildersClubFurnitures { get; init; }

    public DbSet<BadgeDefinitionEntity> BadgeDefinitions { get; init; }
    public DbSet<PlayerBadgeEntity> PlayerBadges { get; init; }

    public DbSet<PlayerUnseenItemEntity> PlayerUnseenItems { get; init; }

    public DbSet<PlayerCurrencyEntity> PlayerCurrencies { get; init; }

    public DbSet<PlayerOutfitEntity> PlayerOutfits { get; init; }

    public DbSet<PlayerSettingsEntity> PlayerSettings { get; init; }

    public DbSet<PlayerSubscriptionEntity> PlayerSubscriptions { get; init; }

    public DbSet<PlayerClubGiftEntity> PlayerClubGifts { get; init; }
    public DbSet<PlayerBonusRareProgressEntity> PlayerBonusRareProgress { get; init; }
    public DbSet<PlayerEntity> Players { get; init; }

    public DbSet<RoomBanEntity> RoomBans { get; init; }

    public DbSet<RoomEntity> Rooms { get; init; }

    public DbSet<RoomModelEntity> RoomModels { get; init; }

    public DbSet<RoomMuteEntity> RoomMutes { get; init; }

    public DbSet<RoomRightEntity> RoomRights { get; init; }

    public DbSet<RoomEntryLogEntity> RoomEntryLogs { get; init; }

    public DbSet<RoomChatlogEntity> Chatlogs { get; init; }
    public DbSet<CommandLogEntity> CommandLogs { get; init; }
    public DbSet<PlayerSanctionEntity> PlayerSanctions { get; init; }
    public DbSet<SecurityTicketEntity> SecurityTickets { get; init; }
    public DbSet<PlayerDiscordLinkEntity> PlayerDiscordLinks { get; init; }
    public DbSet<WebSessionEntity> WebSessions { get; init; }
    public DbSet<AdminPasskeyEntity> AdminPasskeys { get; init; }

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

    public DbSet<PlayerNavigatorSavedSearchEntity> PlayerNavigatorSavedSearches { get; init; }

    public DbSet<PlayerNavigatorCollapsedCategoryEntity> PlayerNavigatorCollapsedCategories { get; init; }

    public DbSet<PlayerNavigatorViewModeEntity> PlayerNavigatorViewModes { get; init; }

    public DbSet<RoomRatingEntity> RoomRatings { get; init; }

    public DbSet<RoomEventEntity> RoomEvents { get; init; }
    public DbSet<RoomFilterWordEntity> RoomFilterWords { get; init; }
    public DbSet<PetEntity> Pets { get; init; }
    public DbSet<PetBreedEntity> PetBreeds { get; init; }
    public DbSet<BotEntity> Bots { get; init; }

    public DbSet<GuildEntity> Guilds { get; init; }
    public DbSet<GuildMemberEntity> GuildMembers { get; init; }
    public DbSet<GuildBadgePartEntity> GuildBadgeParts { get; init; }
    public DbSet<GuildColorEntity> GuildColors { get; init; }

    public DbSet<PermissionGroupEntity> PermissionGroups { get; init; }
    public DbSet<PermissionGroupParentEntity> PermissionGroupParents { get; init; }
    public DbSet<PermissionGroupNodeEntity> PermissionGroupNodes { get; init; }
    public DbSet<PermissionGroupMetaEntity> PermissionGroupMeta { get; init; }
    public DbSet<PlayerPermissionGroupEntity> PlayerPermissionGroups { get; init; }
    public DbSet<PlayerPermissionNodeEntity> PlayerPermissionNodes { get; init; }
    public DbSet<PlayerPermissionMetaEntity> PlayerPermissionMeta { get; init; }
    public DbSet<PermissionAuditEntity> PermissionAudit { get; init; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
    }
}
