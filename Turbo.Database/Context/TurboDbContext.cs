using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Admin;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Entities.Badges;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Games;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Habbicons;
using Turbo.Database.Entities.Hotel;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Polls;
using Turbo.Database.Entities.Quests;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;
using Turbo.Database.Entities.Settings;
using Turbo.Database.Entities.Sound;
using Turbo.Database.Entities.Tracking;
using Turbo.Database.Entities.WiredLink;
using Turbo.Database.Entities.WiredTrading;

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
    public DbSet<WalletCreditReceiptEntity> WalletCreditReceipts { get; init; }
    public DbSet<HumanRespectOperationEntity> HumanRespectOperations { get; init; }
    public DbSet<HumanRespectParticipantReceiptEntity> HumanRespectParticipantReceipts { get; init; }
    public DbSet<PetNutritionOperationEntity> PetNutritionOperations { get; init; }
    public DbSet<PetRespectOperationEntity> PetRespectOperations { get; init; }

    public DbSet<CatalogBackupEntity> CatalogBackups { get; init; }

    public DbSet<CatalogOfferEntity> CatalogOffers { get; init; }

    public DbSet<CatalogFeaturedItemEntity> CatalogFeaturedItems { get; init; }

    public DbSet<CurrencyTypeEntity> CurrencyTypes { get; init; }

    public DbSet<CatalogPageEntity> CatalogPages { get; init; }

    public DbSet<CatalogProductEntity> CatalogProducts { get; init; }
    public DbSet<FurnitureDefinitionEntity> FurnitureDefinitions { get; init; }
    public DbSet<HabboReleaseEntity> HabboReleases { get; init; }
    public DbSet<HabboFurnitureEntity> HabboFurniture { get; init; }
    public DbSet<HabboFurnitureAssetEntity> HabboFurnitureAssets { get; init; }
    public DbSet<AssetBundleEntity> AssetBundles { get; init; }
    public DbSet<AssetPublishTargetEntity> AssetPublishTargets { get; init; }
    public DbSet<AssetPublishedFileEntity> AssetPublishedFiles { get; init; }
    public DbSet<AssetPublishEntity> AssetPublishes { get; init; }
    public DbSet<AssetBrokenReportEntity> AssetBrokenReports { get; init; }
    public DbSet<AssetBrokenIgnoreEntity> AssetBrokenIgnores { get; init; }
    public DbSet<HabboTextVersionEntity> HabboTextVersions { get; init; }
    public DbSet<HabboTextEntity> HabboTexts { get; init; }
    public DbSet<HabboVariableVersionEntity> HabboVariableVersions { get; init; }
    public DbSet<HabboVariableEntity> HabboVariables { get; init; }
    public DbSet<GamedataTextEntity> GamedataTexts { get; init; }
    public DbSet<HabboProductVersionEntity> HabboProductVersions { get; init; }
    public DbSet<HabboProductEntity> HabboProducts { get; init; }
    public DbSet<GamedataProductEntity> GamedataProducts { get; init; }
    public DbSet<HabboFigureVersionEntity> HabboFigureVersions { get; init; }
    public DbSet<HabboFigureEntity> HabboFigures { get; init; }
    public DbSet<GamedataFigureEntity> GamedataFigures { get; init; }
    public DbSet<GamedataVariableEntity> GamedataVariables { get; init; }
    public DbSet<GamedataPluginValueEntity> GamedataPluginValues { get; init; }
    public DbSet<ServerSettingEntity> ServerSettings { get; init; }
    public DbSet<ServerSettingChangeEntity> ServerSettingChanges { get; init; }
    public DbSet<GamedataChangeSetEntity> GamedataChangeSets { get; init; }
    public DbSet<GamedataChangeEntity> GamedataChanges { get; init; }
    public DbSet<GamedataBuildEntity> GamedataBuilds { get; init; }

    public DbSet<FurnitureEntity> Furnitures { get; init; }

    public DbSet<BuildersClubFurnitureEntity> BuildersClubFurnitures { get; init; }

    public DbSet<SongEntity> Songs { get; init; }

    public DbSet<WiredChestEntity> WiredChests { get; init; }
    public DbSet<WiredChestTransactionEntity> WiredChestTransactions { get; init; }
    public DbSet<WiredChestTransactionEntryEntity> WiredChestTransactionEntries { get; init; }
    public DbSet<WiredLinkNetworkEntity> WiredLinkNetworks { get; init; }
    public DbSet<WiredLinkEndpointEntity> WiredLinkEndpoints { get; init; }

    public DbSet<BadgeDefinitionEntity> BadgeDefinitions { get; init; }
    public DbSet<PlayerBadgeEntity> PlayerBadges { get; init; }
    public DbSet<PlayerEffectEntity> PlayerEffects { get; init; }

    public DbSet<PlayerUnseenItemEntity> PlayerUnseenItems { get; init; }

    public DbSet<PlayerPendingNotificationEntity> PlayerPendingNotifications { get; init; }

    public DbSet<PlayerCurrencyEntity> PlayerCurrencies { get; init; }

    public DbSet<PlayerOutfitEntity> PlayerOutfits { get; init; }
    public DbSet<PlayerFigureSetEntity> PlayerFigureSets { get; init; }
    public DbSet<PlayerBoundClothingEntity> PlayerBoundClothing { get; init; }

    public DbSet<PlayerSettingsEntity> PlayerSettings { get; init; }

    public DbSet<PlayerSubscriptionEntity> PlayerSubscriptions { get; init; }

    public DbSet<PlayerClubGiftEntity> PlayerClubGifts { get; init; }
    public DbSet<PlayerProductClaimEntity> PlayerProductClaims { get; init; }
    public DbSet<PlayerIncomeRewardEntity> PlayerIncomeRewards { get; init; }
    public DbSet<PlayerBonusRareProgressEntity> PlayerBonusRareProgress { get; init; }
    public DbSet<PlayerOnlineRewardEntity> PlayerOnlineRewards { get; init; }

    public DbSet<BonusRareCampaignEntity> BonusRareCampaigns { get; init; }

    public DbSet<BonusRareReceiptEntity> BonusRareReceipts { get; init; }

    public DbSet<CatalogPageExpiryEntity> CatalogPageExpiries { get; init; }

    public DbSet<VoucherEntity> Vouchers { get; init; }

    public DbSet<VoucherRedemptionEntity> VoucherRedemptions { get; init; }

    public DbSet<MarketplaceOfferEntity> MarketplaceOffers { get; init; }

    public DbSet<MarketplaceTokenEntity> MarketplaceTokens { get; init; }
    public DbSet<PlayerEntity> Players { get; init; }

    public DbSet<RoomBanEntity> RoomBans { get; init; }

    public DbSet<RoomEntity> Rooms { get; init; }

    public DbSet<StarterRoomEntity> StarterRooms { get; init; }

    public DbSet<RoomModelEntity> RoomModels { get; init; }

    public DbSet<RoomMuteEntity> RoomMutes { get; init; }

    public DbSet<CfhTopicEntity> CfhTopics { get; init; }

    public DbSet<DailyTaskDefinitionEntity> DailyTaskDefinitions { get; init; }

    public DbSet<PlayerDailyTaskEntity> PlayerDailyTasks { get; init; }

    public DbSet<PlayerRewardTrackEntity> PlayerRewardTracks { get; init; }

    public DbSet<RewardTrackEntity> RewardTracks { get; init; }

    public DbSet<RewardTrackTaskEntity> RewardTrackTasks { get; init; }

    public DbSet<RewardTrackTaskLevelEntity> RewardTrackTaskLevels { get; init; }

    public DbSet<RewardTrackPrizeEntity> RewardTrackPrizes { get; init; }

    public DbSet<PlayerHabbiconEntity> PlayerHabbicons { get; init; }

    public DbSet<RoomPollEntity> RoomPolls { get; init; }

    public DbSet<RoomPollQuestionEntity> RoomPollQuestions { get; init; }

    public DbSet<RoomPollChoiceEntity> RoomPollChoices { get; init; }

    public DbSet<PlayerRoomPollEntity> PlayerRoomPolls { get; init; }

    public DbSet<RoomPollAnswerEntity> RoomPollAnswers { get; init; }

    public DbSet<PlayerTargetedOfferEntity> PlayerTargetedOffers { get; init; }

    public DbSet<QuestCampaignEntity> QuestCampaigns { get; init; }

    public DbSet<QuestEntity> Quests { get; init; }

    public DbSet<PlayerQuestEntity> PlayerQuests { get; init; }

    public DbSet<GuildForumEntity> GuildForums { get; init; }

    public DbSet<GuildForumThreadEntity> GuildForumThreads { get; init; }

    public DbSet<GuildForumMessageEntity> GuildForumMessages { get; init; }

    public DbSet<GuildForumReadMarkerEntity> GuildForumReadMarkers { get; init; }

    public DbSet<CfhReportEntity> CfhReports { get; init; }

    public DbSet<CfhReportChatLineEntity> CfhReportChatLines { get; init; }

    public DbSet<ModeratorToolPreferencesEntity> ModeratorToolPreferences { get; init; }

    public DbSet<RoomRaidProtectionEntity> RoomRaidProtections { get; init; }

    public DbSet<RoomRightEntity> RoomRights { get; init; }

    public DbSet<RoomEntryLogEntity> RoomEntryLogs { get; init; }

    public DbSet<RoomChatlogEntity> Chatlogs { get; init; }
    public DbSet<CommandLogEntity> CommandLogs { get; init; }
    public DbSet<RoomWiredLogEntity> RoomWiredLogs { get; init; }
    public DbSet<PlayerSanctionEntity> PlayerSanctions { get; init; }
    public DbSet<FilterWordEntity> FilterWords { get; init; }
    public DbSet<SecurityTicketEntity> SecurityTickets { get; init; }
    public DbSet<PlayerDiscordLinkEntity> PlayerDiscordLinks { get; init; }
    public DbSet<WebSessionEntity> WebSessions { get; init; }
    public DbSet<HotelSettingEntity> HotelSettings { get; init; }

    public DbSet<PromoArticleEntity> PromoArticles { get; init; }

    public DbSet<CommunityGoalEntity> CommunityGoals { get; init; }

    public DbSet<CommunityGoalContributionEntity> CommunityGoalContributions { get; init; }
    public DbSet<AdminPasskeyEntity> AdminPasskeys { get; init; }
    public DbSet<AdminSessionEntity> AdminSessions { get; init; }

    public DbSet<NavigatorTopLevelContextEntity> NavigatorTopLevelContexts { get; init; }

    public DbSet<NavigatorFlatCategoryEntity> NavigatorFlatCategories { get; init; }

    public DbSet<NavigatorEventCategoryEntity> NavigatorEventCategories { get; init; }

    public DbSet<PlayerChatStyleEntity> PlayerChatStyles { get; init; }
    public DbSet<PlayerChatStyleOwnedEntity> PlayerOwnedChatStyles { get; init; }
    public DbSet<PlayerChatStyleBitmapEntity> PlayerChatStyleBitmaps { get; init; }
    public DbSet<ChatStyleLibraryBitmapEntity> ChatStyleLibraryBitmaps { get; init; }
    public DbSet<PlayerCustomFilterWordEntity> PlayerCustomFilterWords { get; init; }

    public DbSet<GamePlayerResultEntity> GamePlayerResults { get; init; }

    public DbSet<PerformanceLogEntity> PerformanceLogs { get; init; }

    public DbSet<ClientPerformanceLogEntity> ClientPerformanceLogs { get; init; }
    public DbSet<ClientLatencyReportEntity> ClientLatencyReports { get; init; }
    public DbSet<ClientLagWarningEntity> ClientLagWarnings { get; init; }
    public DbSet<ClientEventLogEntity> ClientEventLogs { get; init; }
    public DbSet<ClientLoginStepEntity> ClientLoginSteps { get; init; }
    public DbSet<ClientTelemetryDailyEntity> ClientTelemetryDaily { get; init; }

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
    public DbSet<PetSpeechEntity> PetSpeech { get; init; }
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

    public DbSet<AssetMovementOutboxEntity> AssetMovementOutbox { get; init; }
    public DbSet<AssetReversalReceiptEntity> AssetReversalReceipts { get; init; }
    public DbSet<CurrencyLedgerEntryEntity> CurrencyLedger { get; init; }
    public DbSet<MovementConsumerCursorEntity> MovementConsumerCursors { get; init; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // A voucher outlives the furniture or currency it gave: it gives that no more.
        mb.Entity<VoucherEntity>()
            .HasOne(x => x.FurnitureDefinitionEntity)
            .WithMany()
            .HasForeignKey(x => x.FurnitureDefinitionEntityId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<VoucherEntity>()
            .HasOne(x => x.CurrencyTypeEntity)
            .WithMany()
            .HasForeignKey(x => x.CurrencyTypeEntityId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<GamedataTextEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataTextEntity.KEY_COLLATION);
        mb.Entity<HabboTextEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataTextEntity.KEY_COLLATION);
        mb.Entity<HabboVariableEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataVariableEntity.KEY_COLLATION);
        mb.Entity<GamedataProductEntity>()
            .Property(x => x.Code)
            .UseCollation(GamedataProductEntity.CODE_COLLATION);
        mb.Entity<HabboProductEntity>()
            .Property(x => x.Code)
            .UseCollation(GamedataProductEntity.CODE_COLLATION);
        mb.Entity<GamedataFigureEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataProductEntity.CODE_COLLATION);
        mb.Entity<HabboFigureEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataProductEntity.CODE_COLLATION);
        // Habbo's categories include "Trading" and "TRADING": their search codes must stay apart.
        mb.Entity<PlayerNavigatorCollapsedCategoryEntity>()
            .Property(x => x.SearchCode)
            .UseCollation(GamedataProductEntity.CODE_COLLATION);
        mb.Entity<PlayerNavigatorViewModeEntity>()
            .Property(x => x.SearchCode)
            .UseCollation(GamedataProductEntity.CODE_COLLATION);
        mb.Entity<GamedataVariableEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataVariableEntity.KEY_COLLATION);
        mb.Entity<GamedataPluginValueEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataVariableEntity.KEY_COLLATION);
    }
}
