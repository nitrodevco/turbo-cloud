using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Drops <c>deleted_at</c> from every table and <c>updated_at</c> from all but
    /// <c>players</c> and <c>player_subscriptions</c>, the only ones read. Nothing soft-deleted a
    /// row, and <c>deleted_at</c> defaulted to (and updated to) the current time, so every row
    /// looked deleted and the queries that skipped deleted rows (owned pets, room rankings and
    /// floor heights for achievements) found nothing at all.
    /// </summary>
    public partial class DropDeletedAtAndUnreadUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "deleted_at", table: "web_sessions");

            migrationBuilder.DropColumn(name: "updated_at", table: "web_sessions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "security_tickets");

            migrationBuilder.DropColumn(name: "updated_at", table: "security_tickets");

            migrationBuilder.DropColumn(name: "deleted_at", table: "rooms");

            migrationBuilder.DropColumn(name: "updated_at", table: "rooms");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_rights");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_rights");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_ratings");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_ratings");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_mutes");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_mutes");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_models");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_models");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_filter_words");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_filter_words");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_events");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_events");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_entry_logs");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_entry_logs");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_chatlogs");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_chatlogs");

            migrationBuilder.DropColumn(name: "deleted_at", table: "room_bans");

            migrationBuilder.DropColumn(name: "updated_at", table: "room_bans");

            migrationBuilder.DropColumn(name: "deleted_at", table: "players");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_unseen_items");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_unseen_items");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_subscriptions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_settings");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_settings");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_sanctions");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_sanctions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_permission_nodes");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_permission_nodes");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_permission_meta");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_permission_meta");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_permission_groups");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_permission_groups");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_outfits");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_outfits");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_navigator_view_modes");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_navigator_view_modes");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "player_navigator_saved_searches"
            );

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "player_navigator_saved_searches"
            );

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "player_navigator_collapsed_categories"
            );

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "player_navigator_collapsed_categories"
            );

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_figure_sets");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_figure_sets");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_favorite_rooms");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_favorite_rooms");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_effects");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_effects");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_discord_links");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_discord_links");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_currencies");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_currencies");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_club_gifts");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_club_gifts");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_chat_styles_owned");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_chat_styles_owned");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_bonus_rare_progress");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_bonus_rare_progress");

            migrationBuilder.DropColumn(name: "deleted_at", table: "player_badges");

            migrationBuilder.DropColumn(name: "updated_at", table: "player_badges");

            migrationBuilder.DropColumn(name: "deleted_at", table: "pets");

            migrationBuilder.DropColumn(name: "updated_at", table: "pets");

            migrationBuilder.DropColumn(name: "deleted_at", table: "pet_breeds");

            migrationBuilder.DropColumn(name: "updated_at", table: "pet_breeds");

            migrationBuilder.DropColumn(name: "deleted_at", table: "permission_groups");

            migrationBuilder.DropColumn(name: "updated_at", table: "permission_groups");

            migrationBuilder.DropColumn(name: "deleted_at", table: "permission_group_parents");

            migrationBuilder.DropColumn(name: "updated_at", table: "permission_group_parents");

            migrationBuilder.DropColumn(name: "deleted_at", table: "permission_group_nodes");

            migrationBuilder.DropColumn(name: "updated_at", table: "permission_group_nodes");

            migrationBuilder.DropColumn(name: "deleted_at", table: "permission_group_meta");

            migrationBuilder.DropColumn(name: "updated_at", table: "permission_group_meta");

            migrationBuilder.DropColumn(name: "deleted_at", table: "permission_audit");

            migrationBuilder.DropColumn(name: "updated_at", table: "permission_audit");

            migrationBuilder.DropColumn(name: "deleted_at", table: "navigator_top_level_contexts");

            migrationBuilder.DropColumn(name: "updated_at", table: "navigator_top_level_contexts");

            migrationBuilder.DropColumn(name: "deleted_at", table: "navigator_flatcats");

            migrationBuilder.DropColumn(name: "updated_at", table: "navigator_flatcats");

            migrationBuilder.DropColumn(name: "deleted_at", table: "navigator_eventcats");

            migrationBuilder.DropColumn(name: "updated_at", table: "navigator_eventcats");

            migrationBuilder.DropColumn(name: "deleted_at", table: "messenger_requests");

            migrationBuilder.DropColumn(name: "updated_at", table: "messenger_requests");

            migrationBuilder.DropColumn(name: "deleted_at", table: "messenger_messages");

            migrationBuilder.DropColumn(name: "updated_at", table: "messenger_messages");

            migrationBuilder.DropColumn(name: "deleted_at", table: "messenger_ignored");

            migrationBuilder.DropColumn(name: "updated_at", table: "messenger_ignored");

            migrationBuilder.DropColumn(name: "deleted_at", table: "messenger_friends");

            migrationBuilder.DropColumn(name: "updated_at", table: "messenger_friends");

            migrationBuilder.DropColumn(name: "deleted_at", table: "messenger_categories");

            migrationBuilder.DropColumn(name: "updated_at", table: "messenger_categories");

            migrationBuilder.DropColumn(name: "deleted_at", table: "messenger_blocked");

            migrationBuilder.DropColumn(name: "updated_at", table: "messenger_blocked");

            migrationBuilder.DropColumn(name: "deleted_at", table: "ltd_series");

            migrationBuilder.DropColumn(name: "updated_at", table: "ltd_series");

            migrationBuilder.DropColumn(name: "deleted_at", table: "ltd_raffle_entries");

            migrationBuilder.DropColumn(name: "updated_at", table: "ltd_raffle_entries");

            migrationBuilder.DropColumn(name: "deleted_at", table: "hotel_settings");

            migrationBuilder.DropColumn(name: "updated_at", table: "hotel_settings");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_texts");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_texts");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_text_versions");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_text_versions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_releases");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_releases");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_products");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_products");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_product_versions");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_product_versions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_furniture_assets");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_furniture_assets");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_furniture");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_furniture");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_figure_versions");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_figure_versions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "habbo_figure_records");

            migrationBuilder.DropColumn(name: "updated_at", table: "habbo_figure_records");

            migrationBuilder.DropColumn(name: "deleted_at", table: "guilds");

            migrationBuilder.DropColumn(name: "updated_at", table: "guilds");

            migrationBuilder.DropColumn(name: "deleted_at", table: "guild_members");

            migrationBuilder.DropColumn(name: "updated_at", table: "guild_members");

            migrationBuilder.DropColumn(name: "deleted_at", table: "guild_colors");

            migrationBuilder.DropColumn(name: "updated_at", table: "guild_colors");

            migrationBuilder.DropColumn(name: "deleted_at", table: "guild_badge_parts");

            migrationBuilder.DropColumn(name: "updated_at", table: "guild_badge_parts");

            migrationBuilder.DropColumn(name: "deleted_at", table: "gamedata_variables");

            migrationBuilder.DropColumn(name: "updated_at", table: "gamedata_variables");

            migrationBuilder.DropColumn(name: "deleted_at", table: "gamedata_texts");

            migrationBuilder.DropColumn(name: "updated_at", table: "gamedata_texts");

            migrationBuilder.DropColumn(name: "deleted_at", table: "gamedata_products");

            migrationBuilder.DropColumn(name: "updated_at", table: "gamedata_products");

            migrationBuilder.DropColumn(name: "deleted_at", table: "gamedata_figure_records");

            migrationBuilder.DropColumn(name: "updated_at", table: "gamedata_figure_records");

            migrationBuilder.DropColumn(name: "deleted_at", table: "gamedata_changes");

            migrationBuilder.DropColumn(name: "updated_at", table: "gamedata_changes");

            migrationBuilder.DropColumn(name: "deleted_at", table: "gamedata_change_sets");

            migrationBuilder.DropColumn(name: "updated_at", table: "gamedata_change_sets");

            migrationBuilder.DropColumn(name: "deleted_at", table: "gamedata_builds");

            migrationBuilder.DropColumn(name: "updated_at", table: "gamedata_builds");

            migrationBuilder.DropColumn(name: "deleted_at", table: "furniture_definitions");

            migrationBuilder.DropColumn(name: "updated_at", table: "furniture_definitions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "furniture");

            migrationBuilder.DropColumn(name: "updated_at", table: "furniture");

            migrationBuilder.DropColumn(name: "deleted_at", table: "currency_types");

            migrationBuilder.DropColumn(name: "updated_at", table: "currency_types");

            migrationBuilder.DropColumn(name: "deleted_at", table: "command_logs");

            migrationBuilder.DropColumn(name: "updated_at", table: "command_logs");

            migrationBuilder.DropColumn(name: "deleted_at", table: "catalog_products");

            migrationBuilder.DropColumn(name: "updated_at", table: "catalog_products");

            migrationBuilder.DropColumn(name: "deleted_at", table: "catalog_pages");

            migrationBuilder.DropColumn(name: "updated_at", table: "catalog_pages");

            migrationBuilder.DropColumn(name: "deleted_at", table: "catalog_offers");

            migrationBuilder.DropColumn(name: "updated_at", table: "catalog_offers");

            migrationBuilder.DropColumn(name: "deleted_at", table: "catalog_featured_items");

            migrationBuilder.DropColumn(name: "updated_at", table: "catalog_featured_items");

            migrationBuilder.DropColumn(name: "deleted_at", table: "bots");

            migrationBuilder.DropColumn(name: "updated_at", table: "bots");

            migrationBuilder.DropColumn(name: "deleted_at", table: "badge_definitions");

            migrationBuilder.DropColumn(name: "updated_at", table: "badge_definitions");

            migrationBuilder.DropColumn(name: "deleted_at", table: "admin_passkeys");

            migrationBuilder.DropColumn(name: "updated_at", table: "admin_passkeys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "web_sessions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "web_sessions",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "security_tickets",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "security_tickets",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "rooms",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "rooms",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_rights",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_rights",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_ratings",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_ratings",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_mutes",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_mutes",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_models",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_models",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_filter_words",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_filter_words",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_events",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_events",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_entry_logs",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_entry_logs",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_chatlogs",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_chatlogs",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "room_bans",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "room_bans",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "players",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_unseen_items",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_unseen_items",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_subscriptions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_settings",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_settings",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_sanctions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_sanctions",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_permission_nodes",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_permission_nodes",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_permission_meta",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_permission_meta",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_permission_groups",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_permission_groups",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_outfits",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_outfits",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_navigator_view_modes",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_navigator_view_modes",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_navigator_saved_searches",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_navigator_saved_searches",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_navigator_collapsed_categories",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_navigator_collapsed_categories",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_figure_sets",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_figure_sets",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_favorite_rooms",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_favorite_rooms",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_effects",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_effects",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_discord_links",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_discord_links",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_currencies",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_currencies",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_club_gifts",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_club_gifts",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_chat_styles_owned",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_chat_styles_owned",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_chat_styles",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_chat_styles",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_bonus_rare_progress",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_bonus_rare_progress",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "player_badges",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "player_badges",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "pets",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "pets",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "pet_breeds",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "pet_breeds",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "permission_groups",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "permission_groups",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "permission_group_parents",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "permission_group_parents",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "permission_group_nodes",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "permission_group_nodes",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "permission_group_meta",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "permission_group_meta",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "permission_audit",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "permission_audit",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "navigator_top_level_contexts",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "navigator_top_level_contexts",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "navigator_flatcats",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "navigator_flatcats",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "navigator_eventcats",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "navigator_eventcats",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "messenger_requests",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "messenger_requests",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "messenger_messages",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "messenger_messages",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "messenger_ignored",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "messenger_ignored",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "messenger_friends",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "messenger_friends",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "messenger_categories",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "messenger_categories",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "messenger_blocked",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "messenger_blocked",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "ltd_series",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "ltd_series",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "ltd_raffle_entries",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "ltd_raffle_entries",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "hotel_settings",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "hotel_settings",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_texts",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_texts",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_text_versions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_text_versions",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_releases",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_releases",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_products",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_products",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_product_versions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_product_versions",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_furniture_assets",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_furniture_assets",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_furniture",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_furniture",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_figure_versions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_figure_versions",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "habbo_figure_records",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "habbo_figure_records",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "guilds",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "guilds",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "guild_members",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "guild_members",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "guild_colors",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "guild_colors",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "guild_badge_parts",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "guild_badge_parts",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "gamedata_variables",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "gamedata_variables",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "gamedata_texts",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "gamedata_texts",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "gamedata_products",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "gamedata_products",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "gamedata_figure_records",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "gamedata_figure_records",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "gamedata_changes",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "gamedata_changes",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "gamedata_change_sets",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "gamedata_change_sets",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "gamedata_builds",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "gamedata_builds",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "furniture_definitions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "furniture_definitions",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "furniture",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "furniture",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "currency_types",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "currency_types",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "command_logs",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "command_logs",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "catalog_products",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "catalog_products",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "catalog_pages",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "catalog_pages",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "catalog_offers",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "catalog_offers",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "catalog_featured_items",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "catalog_featured_items",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "bots",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "bots",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "badge_definitions",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "badge_definitions",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "deleted_at",
                    table: "admin_passkeys",
                    type: "datetime(6)",
                    nullable: true
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );

            migrationBuilder
                .AddColumn<DateTime>(
                    name: "updated_at",
                    table: "admin_passkeys",
                    type: "datetime(6)",
                    nullable: false
                )
                .Annotation(
                    "MySql:ValueGenerationStrategy",
                    MySqlValueGenerationStrategy.ComputedColumn
                );
        }
    }
}
