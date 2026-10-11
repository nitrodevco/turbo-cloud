using System.Linq;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Admin;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Entities.Badges;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Hotel;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Quests;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;
using Turbo.Database.Entities.Settings;
using Turbo.Database.Entities.Sound;
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
    public DbSet<PlayerRespectEntity> PlayerRespects { get; init; }
    public DbSet<PlayerRespectChangeEntity> PlayerRespectChanges { get; init; }
    public DbSet<PetFeedingEntity> PetFeedings { get; init; }
    public DbSet<PetRespectEntity> PetRespects { get; init; }

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
    public DbSet<HabboTextVersionEntity> HabboTextVersions { get; init; }
    public DbSet<HabboTextEntity> HabboTexts { get; init; }
    public DbSet<GamedataTextEntity> GamedataTexts { get; init; }
    public DbSet<HabboProductVersionEntity> HabboProductVersions { get; init; }
    public DbSet<HabboProductEntity> HabboProducts { get; init; }
    public DbSet<GamedataProductEntity> GamedataProducts { get; init; }
    public DbSet<HabboFigureVersionEntity> HabboFigureVersions { get; init; }
    public DbSet<HabboFigureEntity> HabboFigures { get; init; }
    public DbSet<GamedataFigureEntity> GamedataFigures { get; init; }
    public DbSet<GamedataVariableEntity> GamedataVariables { get; init; }
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

    public DbSet<BadgeDefinitionEntity> BadgeDefinitions { get; init; }
    public DbSet<PlayerBadgeEntity> PlayerBadges { get; init; }
    public DbSet<PlayerEffectEntity> PlayerEffects { get; init; }

    public DbSet<PlayerUnseenItemEntity> PlayerUnseenItems { get; init; }

    public DbSet<PlayerCurrencyEntity> PlayerCurrencies { get; init; }
    public DbSet<PlayerCurrencyLedgerEntity> PlayerCurrencyLedger { get; init; }

    public DbSet<PlayerOutfitEntity> PlayerOutfits { get; init; }
    public DbSet<PlayerFigureSetEntity> PlayerFigureSets { get; init; }
    public DbSet<PlayerBoundClothingEntity> PlayerBoundClothing { get; init; }

    public DbSet<PlayerSettingsEntity> PlayerSettings { get; init; }

    public DbSet<PlayerSubscriptionEntity> PlayerSubscriptions { get; init; }
    public DbSet<PlayerSubscriptionGrantEntity> PlayerSubscriptionGrants { get; init; }
    public DbSet<PlayerLoginEntity> PlayerLogins { get; init; }
    public DbSet<FurnitureTransferEntity> FurnitureTransfers { get; init; }

    public DbSet<PlayerClubGiftEntity> PlayerClubGifts { get; init; }
    public DbSet<PlayerBonusRareProgressEntity> PlayerBonusRareProgress { get; init; }

    public DbSet<BonusRareCampaignEntity> BonusRareCampaigns { get; init; }

    public DbSet<BonusRareReceiptEntity> BonusRareReceipts { get; init; }

    public DbSet<CatalogPageExpiryEntity> CatalogPageExpiries { get; init; }

    public DbSet<VoucherEntity> Vouchers { get; init; }

    public DbSet<VoucherRedemptionEntity> VoucherRedemptions { get; init; }
    public DbSet<PlayerEntity> Players { get; init; }

    public DbSet<RoomBanEntity> RoomBans { get; init; }

    public DbSet<RoomEntity> Rooms { get; init; }

    public DbSet<RoomModelEntity> RoomModels { get; init; }

    public DbSet<RoomMuteEntity> RoomMutes { get; init; }

    public DbSet<CfhTopicEntity> CfhTopics { get; init; }

    public DbSet<DailyTaskDefinitionEntity> DailyTaskDefinitions { get; init; }

    public DbSet<PlayerDailyTaskEntity> PlayerDailyTasks { get; init; }

    public DbSet<PlayerRewardTrackEntity> PlayerRewardTracks { get; init; }

    public DbSet<QuestCampaignEntity> QuestCampaigns { get; init; }

    public DbSet<QuestEntity> Quests { get; init; }

    public DbSet<PlayerQuestEntity> PlayerQuests { get; init; }

    public DbSet<GuildForumEntity> GuildForums { get; init; }

    public DbSet<GuildForumThreadEntity> GuildForumThreads { get; init; }

    public DbSet<GuildForumMessageEntity> GuildForumMessages { get; init; }

    public DbSet<GuildForumReadMarkerEntity> GuildForumReadMarkers { get; init; }

    public DbSet<CfhReportEntity> CfhReports { get; init; }

    public DbSet<CfhReportChatLineEntity> CfhReportChatLines { get; init; }

    public DbSet<RoomRaidProtectionEntity> RoomRaidProtections { get; init; }

    public DbSet<RoomRightEntity> RoomRights { get; init; }

    public DbSet<RoomEntryLogEntity> RoomEntryLogs { get; init; }

    public DbSet<RoomChatlogEntity> Chatlogs { get; init; }
    public DbSet<CommandLogEntity> CommandLogs { get; init; }
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

    public DbSet<NavigatorTopLevelContextEntity> NavigatorTopLevelContexts { get; init; }

    public DbSet<NavigatorFlatCategoryEntity> NavigatorFlatCategories { get; init; }

    public DbSet<NavigatorEventCategoryEntity> NavigatorEventCategories { get; init; }

    public DbSet<ChatStyleEntity> ChatStyles { get; init; }
    public DbSet<PlayerChatStyleOwnedEntity> PlayerOwnedChatStyles { get; init; }

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

    /// <summary>
    /// What a hotel defines once and many rows use. Deleting one is refused while anything still
    /// uses it: a cascade took every copy of a furni out of every inventory and room, every room
    /// built on a model, and every balance in a currency. The admin panel checks first and says
    /// what still uses it.
    /// </summary>
    private static readonly System.Type[] DEFINITIONS =
    [
        typeof(FurnitureDefinitionEntity),
        typeof(RoomModelEntity),
        typeof(CurrencyTypeEntity),
    ];

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

        // PostgreSQL keeps 63 characters of a name; the one made for this key ran to 64.
        mb.Entity<PlayerDailyTaskEntity>()
            .HasOne(x => x.Definition)
            .WithMany()
            .HasForeignKey(x => x.DefinitionEntityId)
            .HasConstraintName("fk_player_daily_tasks_definition_id");

        mb.Entity<GamedataTextEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataTextEntity.KEY_COLLATION);
        mb.Entity<HabboTextEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataTextEntity.KEY_COLLATION);
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
        mb.Entity<GamedataVariableEntity>()
            .Property(x => x.Key)
            .UseCollation(GamedataVariableEntity.KEY_COLLATION);

        // The recovery timer reads the rows still pending every few seconds. A partial index holds
        // only those, so it stays a handful of entries however large the table grows; a scan of
        // the whole table measured over a second a tick at 2-3 million rows.
        mb.Entity<PlayerRespectEntity>()
            .HasIndex(x => x.Id, "ix_player_respects_pending")
            .HasFilter("status = 0");
        mb.Entity<PetRespectEntity>()
            .HasIndex(x => x.Id, "ix_pet_respects_pending")
            .HasFilter("status = 0");
        mb.Entity<AchievementFactEntity>()
            .HasIndex(x => new { x.PlayerId, x.Id }, "ix_achievement_facts_unprocessed")
            .HasFilter("NOT processed");
        mb.Entity<AchievementProjectionEntity>()
            .HasIndex(x => x.PlayerId, "ix_achievement_projections_publication_pending")
            .HasFilter("publication_pending");
        // The hourly prune finds processed facts past their retention.
        mb.Entity<AchievementFactEntity>()
            .HasIndex(x => x.OccurredAtUtc, "ix_achievement_facts_processed_occurred_at")
            .HasFilter("processed");
        // Each web sign-in deletes the sessions that have ended.
        mb.Entity<WebSessionEntity>().HasIndex(x => x.ExpiresAt);
        // A login replays the messages it hasn't been given, newest first: only undelivered ones.
        mb.Entity<MessengerMessageEntity>()
            .HasIndex(
                x => new { x.ReceiverPlayerEntityId, x.CreatedAt },
                "ix_messenger_messages_undelivered"
            )
            .HasFilter("NOT delivered");
        // Moderators search chat by any part of a line (lower(message) LIKE '%...%'), which no
        // b-tree can serve; the baseline adds a trigram index on lower(message) for it.
        // A serial of an LTD series is sold once.
        mb.Entity<LtdRaffleEntryEntity>()
            .HasIndex(x => new { x.SeriesEntityId, x.SerialNumber })
            .IsUnique()
            .HasFilter("serial_number IS NOT NULL");

        // A page with no images or texts has an empty list, never a missing one.
        mb.Entity<CatalogPageEntity>().Property(x => x.ImageData).HasDefaultValueSql("'{}'");
        mb.Entity<CatalogPageEntity>().Property(x => x.TextData).HasDefaultValueSql("'{}'");

        // Not worn is null, never 0, so "is it worn" has one answer.
        mb.Entity<PlayerBadgeEntity>()
            .ToTable(t =>
                t.HasCheckConstraint("ck_player_badges_slot_id", "slot_id IS NULL OR slot_id > 0")
            );

        // Rules the code keeps, kept by the database too, so a write from anywhere (the panel, a
        // script, a plugin) cannot leave a row the game reads wrongly.
        mb.Entity<PlayerQuestEntity>()
            .HasIndex(x => x.PlayerEntityId, "ix_player_quests_accepted")
            .IsUnique()
            .HasFilter("accepted");
        mb.Entity<GuildMemberEntity>()
            .HasIndex(x => x.PlayerEntityId, "ix_guild_members_favourite")
            .IsUnique()
            .HasFilter("is_favourite");
        mb.Entity<LtdSeriesEntity>()
            .HasIndex(x => x.CatalogProductEntityId, "ix_catalog_ltd_series_active")
            .IsUnique()
            .HasFilter("is_active");
        mb.Entity<LtdSeriesEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "ck_catalog_ltd_series_quantity",
                    "remaining_quantity >= 0 AND remaining_quantity <= total_quantity"
                )
            );
        // A series' raffle entries and serials are the record of who owns which piece; deleting
        // the product or its page must not take them.
        mb.Entity<LtdSeriesEntity>()
            .HasOne(x => x.CatalogProduct)
            .WithMany()
            .HasForeignKey(x => x.CatalogProductEntityId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<CurrencyTypeEntity>()
            .HasIndex(x => new { x.CurrencyType, x.ActivityPointType })
            .IsUnique()
            .AreNullsDistinct(false);
        mb.Entity<CurrencyTypeEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "ck_catalog_currency_types_activity_point_type",
                    "(type = 4) = (activity_point_type IS NOT NULL)"
                )
            );
        mb.Entity<CatalogOfferEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "ck_catalog_offers_currency",
                    "cost_currency = 0 OR currency_type_id IS NOT NULL"
                )
            );
        mb.Entity<VoucherEntity>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_vouchers_uses",
                    "uses >= 0 AND (max_uses IS NULL OR (max_uses >= 1 AND uses <= max_uses))"
                );
                t.HasCheckConstraint(
                    "ck_vouchers_amounts",
                    "credits >= 0 AND currency_amount >= 0 AND furniture_quantity >= 0 "
                        + "AND (currency_amount = 0 OR currency_type_id IS NOT NULL)"
                );
            });
        mb.Entity<WiredChestEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "ck_wired_chests_amounts",
                    "coins >= 0 AND capacity_level >= 0"
                )
            );
        mb.Entity<BonusRareCampaignEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "ck_catalog_bonus_rare_campaigns_window",
                    "ends_at > starts_at AND credits_required > 0"
                )
            );
        mb.Entity<GamedataVariableEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "ck_gamedata_variables_source",
                    "setting_path IS NULL OR linked_file IS NULL"
                )
            );

        // Queries that run often enough to want their own index.
        mb.Entity<BuildersClubFurnitureEntity>().HasIndex(x => x.CatalogOfferEntityId);
        mb.Entity<GuildForumMessageEntity>().HasIndex(x => new { x.GuildEntityId, x.CreatedAt });
        mb.Entity<GuildForumMessageEntity>()
            .HasIndex(x => new { x.GuildEntityId, x.PlayerEntityId });
        mb.Entity<CommandLogEntity>().HasIndex(x => new { x.Outcome, x.Id });
        mb.Entity<RoomEntryLogEntity>().HasIndex(x => new { x.PlayerEntityId, x.CreatedAt });
        mb.Entity<PlayerEntity>().HasIndex(x => x.LastLoginAt);
        mb.Entity<AchievementProgressEntity>()
            .HasIndex(x => x.PlayerId, "ix_achievement_progress_pending_delivery")
            .HasFilter("pending_delivery");

        mb.HasPostgresExtension("pg_trgm");

        // What the code already holds to, held by the database too, so a bug or a hand edit
        // can't leave a balance below nothing or an offer that pays the buyer.
        mb.Entity<PlayerCurrencyEntity>()
            .ToTable(t => t.HasCheckConstraint("ck_player_currencies_amount", "amount >= 0"));
        mb.Entity<CatalogOfferEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "ck_catalog_offers_costs",
                    "cost_credits >= 0 AND cost_currency >= 0"
                )
            );
        mb.Entity<CatalogProductEntity>()
            .ToTable(t => t.HasCheckConstraint("ck_catalog_products_quantity", "quantity >= 1"));

        // Names are looked up by their key, by equality and by prefix (the friend search). In the
        // byte-order C collation a b-tree serves a prefix LIKE; in a language collation it can't.
        mb.Entity<PlayerEntity>().Property(x => x.NameKey).UseCollation("C");
        // The key is the name lowered (names are ASCII), so a write that changes one and not
        // the other is refused rather than leaving a player no lookup finds.
        mb.Entity<PlayerEntity>()
            .ToTable(t =>
                t.HasCheckConstraint("ck_players_name_key", "name_key = lower(trim(name))")
            );

        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            if (
                fk.DeleteBehavior == DeleteBehavior.Cascade
                && DEFINITIONS.Contains(fk.PrincipalEntityType.ClrType)
            )
                fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
