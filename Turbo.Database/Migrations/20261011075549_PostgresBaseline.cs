using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class PostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase().Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "achievement_audit_logs",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    operation_id = table.Column<string>(
                        type: "character varying(160)",
                        maxLength: 160,
                        nullable: false
                    ),
                    actor = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    request_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    occurred_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    before_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    after_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_achievement_audit_logs", x => x.id);
                },
                comment: "Every operator change to the achievements (imports, retiring, enabling), with who made it and why."
            );

            migrationBuilder.CreateTable(
                name: "achievement_definitions",
                columns: table => new
                {
                    achievement_id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    definition_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_achievement_definitions",
                        x => new { x.achievement_id, x.revision }
                    );
                },
                comment: "The hotel's achievements: each one's levels, badge, what counts toward it and its rewards."
            );

            migrationBuilder.CreateTable(
                name: "achievement_distinct_values",
                columns: table => new
                {
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    achievement_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_achievement_distinct_values",
                        x => new
                        {
                            x.player_id,
                            x.achievement_id,
                            x.value,
                        }
                    );
                },
                comment: "One distinct value a player has contributed to a distinct-count achievement."
            );

            migrationBuilder.CreateTable(
                name: "achievement_facts",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    operation_id = table.Column<string>(
                        type: "character varying(160)",
                        maxLength: 160,
                        nullable: false
                    ),
                    occurred_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    fact_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    bindings_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    processed = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_achievement_facts", x => x.id);
                },
                comment: "Things players did that count toward achievements, waiting to be or already turned into progress."
            );

            migrationBuilder.CreateTable(
                name: "achievement_progress",
                columns: table => new
                {
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    achievement_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false),
                    forward_adjustment = table.Column<long>(type: "bigint", nullable: false),
                    current_streak = table.Column<long>(type: "bigint", nullable: false),
                    last_streak_day = table.Column<DateOnly>(type: "date", nullable: true),
                    earned_level = table.Column<int>(type: "integer", nullable: false),
                    completed_level = table.Column<int>(type: "integer", nullable: false),
                    score_earned = table.Column<int>(type: "integer", nullable: false),
                    last_level_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    distinct_count = table.Column<int>(type: "integer", nullable: false),
                    intervals_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    open_awards = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    pending_delivery = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_achievement_progress",
                        x => new { x.player_id, x.achievement_id }
                    );
                },
                comment: "Everything about one player's one achievement: reducer state, the levels earned, and the levels still being delivered or announced."
            );

            migrationBuilder.CreateTable(
                name: "achievement_projections",
                columns: table => new
                {
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    completed_levels = table.Column<int>(type: "integer", nullable: false),
                    publication_pending = table.Column<bool>(type: "boolean", nullable: false),
                    reconciled_stamp = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    observed_state = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_achievement_projections", x => x.player_id);
                },
                comment: "Each player's achievement score and levels as other players see them, and whether that still has to be published."
            );

            migrationBuilder.CreateTable(
                name: "achievement_wallet_receipts",
                columns: table => new
                {
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    award_key = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_achievement_wallet_receipts",
                        x => new { x.player_id, x.award_key }
                    );
                },
                comment: "Currency an achievement level paid a player, one row per payment, so a retry never pays twice."
            );

            migrationBuilder.CreateTable(
                name: "asset_bundles",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    revision = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    source = table.Column<int>(type: "integer", nullable: false),
                    hash = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: true
                    ),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    ids = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    retry = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_bundles", x => x.id);
                },
                comment: "A .nitro bundle the hotel keeps, by its kind and name: the file itself is on disk (bundled/<kind>/<name>.nitro under the asset directory), and this row is what is known of it without opening it."
            );

            migrationBuilder.CreateTable(
                name: "asset_publish_targets",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    protocol = table.Column<int>(type: "integer", nullable: false),
                    host = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    port = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    password = table.Column<byte[]>(type: "bytea", nullable: true),
                    remote_path = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: false
                    ),
                    public_url = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: false
                    ),
                    allow_self_signed = table.Column<bool>(type: "boolean", nullable: false),
                    host_key = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_publish_targets", x => x.id);
                },
                comment: "Where bundles are published: a folder on this server, or an FTP, FTPS or SFTP server."
            );

            migrationBuilder.CreateTable(
                name: "badge_definitions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    badge_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    rarity = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_badge_definitions", x => x.id);
                },
                comment: "What the hotel says about a badge code."
            );

            migrationBuilder.CreateTable(
                name: "catalog_backups",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    automatic = table.Column<bool>(type: "boolean", nullable: false),
                    pages = table.Column<int>(type: "integer", nullable: false),
                    offers = table.Column<int>(type: "integer", nullable: false),
                    products = table.Column<int>(type: "integer", nullable: false),
                    featured_items = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_backups", x => x.id);
                },
                comment: "A copy of the catalog's pages, offers, products and featured items as they were saved when it was taken, which the editor can roll the catalog back to."
            );

            migrationBuilder.CreateTable(
                name: "catalog_bonus_rare_campaigns",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    furniture_name = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    product_code = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    credits_required = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ends_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_bonus_rare_campaigns", x => x.id);
                    table.CheckConstraint(
                        "ck_catalog_bonus_rare_campaigns_window",
                        "ends_at > starts_at AND credits_required > 0"
                    );
                },
                comment: "A bonus rare campaign: a furniture given for every CreditsRequired credits a player brings in, counted as Source says."
            );

            migrationBuilder.CreateTable(
                name: "catalog_currency_types",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    activity_point_type = table.Column<int>(type: "integer", nullable: true),
                    enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_currency_types", x => x.id);
                    table.CheckConstraint(
                        "ck_catalog_currency_types_activity_point_type",
                        "(type = 4) = (activity_point_type IS NOT NULL)"
                    );
                },
                comment: "The currencies a hotel has (credits, duckets, diamonds, ...) and how the client knows them."
            );

            migrationBuilder.CreateTable(
                name: "catalog_featured_items",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    image = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    type = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_featured_items", x => x.id);
                },
                comment: "A featured item of the catalog's front page, which the front page layouts draw: the first large on the left, the next three in a list."
            );

            migrationBuilder.CreateTable(
                name: "catalog_pages",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    localization = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    name = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    icon = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    layout = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false,
                        defaultValue: "default_3x3"
                    ),
                    image_data = table.Column<List<string>>(
                        type: "text[]",
                        nullable: false,
                        defaultValueSql: "'{}'"
                    ),
                    text_data = table.Column<List<string>>(
                        type: "text[]",
                        nullable: false,
                        defaultValueSql: "'{}'"
                    ),
                    sort_order = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    display = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_pages", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalog_pages_catalog_pages_parent_id",
                        column: x => x.parent_id,
                        principalTable: "catalog_pages",
                        principalColumn: "id"
                    );
                },
                comment: "The catalog's page tree, as both the normal and the Builders Club catalog show it."
            );

            migrationBuilder.CreateTable(
                name: "furniture_definitions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    sprite_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    category = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    logic = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false,
                        defaultValue: "none"
                    ),
                    total_states = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    width = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    length = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    stack_height = table.Column<double>(
                        type: "double precision",
                        nullable: false,
                        defaultValue: 0.0
                    ),
                    can_stack = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    can_walk = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    can_sit = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    can_lay = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    can_recycle = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    can_trade = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    can_group = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    can_sell = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    usage_policy = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    extra_data = table.Column<string>(type: "text", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    client_category = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    default_dir = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    part_colors = table.Column<List<string>>(type: "text[]", nullable: true),
                    public_name = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    description = table.Column<string>(type: "text", nullable: true),
                    ad_url = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    custom_params = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    furni_line = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    environment = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    rare = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    excluded_dynamic = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_furniture_definitions", x => x.id);
                },
                comment: "The hotel's furni: one row per item class, with its size, behaviour (logic) and how it may be used."
            );

            migrationBuilder.CreateTable(
                name: "furniture_transfers",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    furniture_id = table.Column<int>(type: "integer", nullable: false),
                    definition_id = table.Column<int>(type: "integer", nullable: false),
                    from_player_id = table.Column<int>(type: "integer", nullable: false),
                    to_player_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<byte>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_furniture_transfers", x => x.id);
                },
                comment: "A furni changing owner between players: which, from whom, to whom, and why."
            );

            migrationBuilder.CreateTable(
                name: "gamedata_builds",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    file = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    hash = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false
                    ),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    size = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamedata_builds", x => x.id);
                },
                comment: "A gamedata file as it was built, by the hash of its content, gzipped."
            );

            migrationBuilder.CreateTable(
                name: "gamedata_change_sets",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    player_id = table.Column<int>(type: "integer", nullable: true),
                    release_id = table.Column<int>(type: "integer", nullable: true),
                    reverts_id = table.Column<int>(type: "integer", nullable: true),
                    rolled_back_by_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamedata_change_sets", x => x.id);
                },
                comment: "Gamedata changes made together - an import, an edit, a rollback - and rolled back together."
            );

            migrationBuilder.CreateTable(
                name: "gamedata_figures",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    record_key = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false,
                        collation: "C"
                    ),
                    record_group = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    data = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamedata_figures", x => x.id);
                },
                comment: "One record of the hotel's figure data - a colour, a kind of clothing or a piece of clothing - its fields as JSON, by the names Habbo's file gives them."
            );

            migrationBuilder.CreateTable(
                name: "gamedata_products",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false,
                        collation: "C"
                    ),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamedata_products", x => x.id);
                },
                comment: "One of the hotel's products, its product data: the name and description the client shows for a catalog offer whose name key (catalog_offers.product_code) is the code."
            );

            migrationBuilder.CreateTable(
                name: "gamedata_texts",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    text_key = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false,
                        collation: "C"
                    ),
                    value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamedata_texts", x => x.id);
                },
                comment: "One of the hotel's texts, the external texts the client shows: Habbo's taken in, and the hotel's own."
            );

            migrationBuilder.CreateTable(
                name: "gamedata_variables",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    variable_key = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false,
                        collation: "C"
                    ),
                    value = table.Column<string>(type: "text", nullable: false),
                    setting_path = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    linked_file = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamedata_variables", x => x.id);
                    table.CheckConstraint(
                        "ck_gamedata_variables_source",
                        "setting_path IS NULL OR linked_file IS NULL"
                    );
                },
                comment: "One of the client's external variables, the configuration it loads from the gamedata host: its key, and its value as JSON (\"text\", true, 120, [1, 2])."
            );

            migrationBuilder.CreateTable(
                name: "guild_badge_parts",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    part_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    part_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    mask_file_name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_badge_parts", x => x.id);
                },
                comment: "A part the badge editor may pick."
            );

            migrationBuilder.CreateTable(
                name: "guild_colors",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    slot = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    color_id = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(
                        type: "character varying(6)",
                        maxLength: 6,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_colors", x => x.id);
                },
                comment: "A colour the badge editor may pick, from one of its three palettes."
            );

            migrationBuilder.CreateTable(
                name: "habbo_figure_versions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    domain = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    hash = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false
                    ),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    set_count = table.Column<int>(type: "integer", nullable: false),
                    color_count = table.Column<int>(type: "integer", nullable: false),
                    checked_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    imported_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_figure_versions", x => x.id);
                },
                comment: "One version of Habbo's figure data (/gamedata/figuredata, XML) as a check found it, gzipped, by its hash."
            );

            migrationBuilder.CreateTable(
                name: "habbo_figures",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    record_key = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false,
                        collation: "C"
                    ),
                    data = table.Column<string>(type: "text", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_figures", x => x.id);
                },
                comment: "One record of Habbo's figure data as it was last taken in."
            );

            migrationBuilder.CreateTable(
                name: "habbo_furniture",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    type = table.Column<int>(type: "integer", nullable: false),
                    class_name = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    sprite_id = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "text", nullable: false),
                    release_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_furniture", x => x.id);
                },
                comment: "One of Habbo's furniture as it was last taken in: its furnidata item, as Habbo wrote it."
            );

            migrationBuilder.CreateTable(
                name: "habbo_furniture_assets",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    asset_name = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    states = table.Column<int>(type: "integer", nullable: false),
                    info = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    retry = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_furniture_assets", x => x.id);
                },
                comment: "What one of Habbo's furniture asset files said, by its asset name (a classname without its *N colour) and the revision furnidata gave it: the file is read once per revision."
            );

            migrationBuilder.CreateTable(
                name: "habbo_furniture_versions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    domain = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    revision = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    furniture_data_hash = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false
                    ),
                    furniture_data = table.Column<byte[]>(type: "bytea", nullable: false),
                    furniture_count = table.Column<int>(type: "integer", nullable: false),
                    checked_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    imported_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_furniture_versions", x => x.id);
                },
                comment: "One of Habbo's releases as a check found it: the revision its external variables named, and the furniture data it served then, gzipped, so an import works from what was checked."
            );

            migrationBuilder.CreateTable(
                name: "habbo_product_versions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    domain = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    hash = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false
                    ),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    product_count = table.Column<int>(type: "integer", nullable: false),
                    checked_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    imported_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_product_versions", x => x.id);
                },
                comment: "One version of Habbo's product data as a check found it, gzipped, by its hash."
            );

            migrationBuilder.CreateTable(
                name: "habbo_products",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false,
                        collation: "C"
                    ),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_products", x => x.id);
                },
                comment: "One of Habbo's products as it was last taken in."
            );

            migrationBuilder.CreateTable(
                name: "habbo_text_versions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    domain = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    hash = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false
                    ),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    text_count = table.Column<int>(type: "integer", nullable: false),
                    checked_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    imported_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_text_versions", x => x.id);
                },
                comment: "One version of Habbo's external texts as a check found it, gzipped, by its hash."
            );

            migrationBuilder.CreateTable(
                name: "habbo_texts",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    text_key = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false,
                        collation: "C"
                    ),
                    value = table.Column<string>(type: "text", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habbo_texts", x => x.id);
                },
                comment: "One of Habbo's texts as it was last taken in."
            );

            migrationBuilder.CreateTable(
                name: "hotel_chat_styles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    club_only = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    ambassador_only = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    staff_only = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    purchasable = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    system = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotel_chat_styles", x => x.id);
                },
                comment: "One chat bubble style the client can draw, and who may speak with it."
            );

            migrationBuilder.CreateTable(
                name: "hotel_community_goals",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ends_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    level_scores = table.Column<List<int>>(type: "integer[]", nullable: false),
                    reward_ranks = table.Column<List<int>>(type: "integer[]", nullable: false),
                    side_one_page_id = table.Column<int>(type: "integer", nullable: true),
                    side_two_page_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotel_community_goals", x => x.id);
                },
                comment: "A community goal: from StartsAt to EndsAt the hotel plays it together, buying from its catalog pages (and, in a voting goal, voting for a side)."
            );

            migrationBuilder.CreateTable(
                name: "hotel_promo_articles",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    title = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    body_text = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: false
                    ),
                    button_text = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    link_type = table.Column<int>(type: "integer", nullable: false),
                    link_content = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: false
                    ),
                    image_url = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: false
                    ),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    visible = table.Column<bool>(type: "boolean", nullable: false),
                    starts_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    ends_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotel_promo_articles", x => x.id);
                },
                comment: "A promo article of the reception's carousel: a title, words, a picture and a button."
            );

            migrationBuilder.CreateTable(
                name: "hotel_settings",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    key = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    value = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotel_settings", x => x.id);
                },
                comment: "One setting for the whole hotel that staff change while it runs, such as the welcome message, by its key."
            );

            migrationBuilder.CreateTable(
                name: "messenger_messages",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    sender_id = table.Column<int>(type: "integer", nullable: false),
                    receiver_id = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    delivered = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messenger_messages", x => x.id);
                },
                comment: "Private messages between friends, and whether each has been delivered."
            );

            migrationBuilder.CreateTable(
                name: "moderation_actions",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    action = table.Column<short>(type: "smallint", nullable: false),
                    source = table.Column<byte>(type: "smallint", nullable: false),
                    actor_id = table.Column<int>(type: "integer", nullable: true),
                    target_player_id = table.Column<int>(type: "integer", nullable: true),
                    room_id = table.Column<int>(type: "integer", nullable: true),
                    guild_id = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    detail = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_actions", x => x.id);
                },
                comment: "Every moderation action: who did what to which player or room, from where, why and until when."
            );

            migrationBuilder.CreateTable(
                name: "moderation_cfh_reports",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    reporter_id = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    extra_data_id = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    item_id = table.Column<int>(type: "integer", nullable: true),
                    guild_id = table.Column<int>(type: "integer", nullable: true),
                    forum_thread_id = table.Column<int>(type: "integer", nullable: true),
                    forum_message_id = table.Column<int>(type: "integer", nullable: true),
                    reported_id = table.Column<int>(type: "integer", nullable: true),
                    room_id = table.Column<int>(type: "integer", nullable: true),
                    topic_id = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: false
                    ),
                    reporter_name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    reporter_email = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    closed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    sanctioned = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    auto_moderated = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    appeal_status = table.Column<byte>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (byte)0
                    ),
                    appeal_created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    appeal_resolved_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_cfh_reports", x => x.id);
                },
                comment: "A call for help."
            );

            migrationBuilder.CreateTable(
                name: "moderation_cfh_topics",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    consequence = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    sort_order = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_cfh_topics", x => x.id);
                },
                comment: "A call for help topic."
            );

            migrationBuilder.CreateTable(
                name: "moderation_command_logs",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: true),
                    player_id = table.Column<int>(type: "integer", nullable: true),
                    command = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    arguments = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    outcome = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: true
                    ),
                    resolved_audience_json = table.Column<string>(type: "jsonb", nullable: true),
                    batch_target_results_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_command_logs", x => x.id);
                },
                comment: "One use of a chat command by an executor who holds command.log, so an operator logs staff and nobody else."
            );

            migrationBuilder.CreateTable(
                name: "moderation_filter_words",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    word = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_filter_words", x => x.id);
                },
                comment: "A word of the hotel's filter, kept out of everything players type (see IWordFilter)."
            );

            migrationBuilder.CreateTable(
                name: "moderation_sanctions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    issuer_id = table.Column<int>(type: "integer", nullable: true),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    revoked_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    revoked_by_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_sanctions", x => x.id);
                },
                comment: "A hotel-wide sanction on a player."
            );

            migrationBuilder.CreateTable(
                name: "navigator_event_categories",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(type: "text", nullable: false),
                    visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_navigator_event_categories", x => x.id);
                },
                comment: "The categories a room event can be listed under in the navigator."
            );

            migrationBuilder.CreateTable(
                name: "navigator_room_categories",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(type: "text", nullable: false),
                    visible = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    automatic = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    automatic_category = table.Column<string>(type: "text", nullable: true),
                    global_category = table.Column<string>(type: "text", nullable: true),
                    staff_only = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    min_rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    required_node = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    order_num = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_navigator_room_categories", x => x.id);
                },
                comment: "The categories a room can be put in, as the navigator lists them."
            );

            migrationBuilder.CreateTable(
                name: "navigator_top_level_contexts",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    search_code = table.Column<string>(type: "text", nullable: false),
                    visible = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    order_num = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_navigator_top_level_contexts", x => x.id);
                },
                comment: "The tabs along the top of the navigator."
            );

            migrationBuilder.CreateTable(
                name: "permission_audit_logs",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    actor_player_id = table.Column<int>(type: "integer", nullable: true),
                    target_type = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    value = table.Column<string>(type: "text", nullable: true),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_audit_logs", x => x.id);
                },
                comment: "One change to a group's or a player's permissions, written in the same save as the change."
            );

            migrationBuilder.CreateTable(
                name: "permission_groups",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    display_name = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    weight = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_groups", x => x.id);
                },
                comment: "A named set of permission nodes and meta that players hold and other groups inherit."
            );

            migrationBuilder.CreateTable(
                name: "pet_breeds",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    palette_id = table.Column<int>(type: "integer", nullable: false),
                    breed_id = table.Column<int>(type: "integer", nullable: false),
                    rarity_level = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    sellable = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    rare = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    color_tag = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: -1
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_breeds", x => x.id);
                },
                comment: "A sellable palette of a pet type: what the catalog offers and what breeding yields."
            );

            migrationBuilder.CreateTable(
                name: "pet_feedings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    pet_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    fed_by_id = table.Column<int>(type: "integer", nullable: false),
                    nutrition_before = table.Column<int>(type: "integer", nullable: false),
                    nutrition_offered = table.Column<int>(type: "integer", nullable: false),
                    nutrition_max = table.Column<int>(type: "integer", nullable: false),
                    nutrition_after = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_feedings", x => x.id);
                },
                comment: "A pet eating or drinking, written with the pet's new nutrition so a retry never feeds twice."
            );

            migrationBuilder.CreateTable(
                name: "pet_respects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    giver_id = table.Column<int>(type: "integer", nullable: false),
                    pet_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    respect_before = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<byte>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (byte)0
                    ),
                    respect_after = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_respects", x => x.id);
                },
                comment: "One scratch a player gave a pet: who, which pet, and whether it went through."
            );

            migrationBuilder.CreateTable(
                name: "pet_speech_lines",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    type_id = table.Column<int>(type: "integer", nullable: true),
                    line = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_speech_lines", x => x.id);
                },
                comment: "A line a pet may say when told to speak or on its own now and then."
            );

            migrationBuilder.CreateTable(
                name: "player_currency_ledger",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    currency_type_id = table.Column<int>(type: "integer", nullable: false),
                    change = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<byte>(type: "smallint", nullable: false),
                    detail = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_currency_ledger", x => x.id);
                },
                comment: "Every change to a player's balance: how much, why, and the balance it left."
            );

            migrationBuilder.CreateTable(
                name: "player_logins",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    machine_id = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    logged_out_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_logins", x => x.id);
                },
                comment: "A session a player opened: from which address and machine, and when it ended."
            );

            migrationBuilder.CreateTable(
                name: "player_respect_changes",
                columns: table => new
                {
                    respect_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    total_after = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_player_respect_changes",
                        x => new { x.respect_id, x.kind }
                    );
                },
                comment: "What a respect did to one player's counters, written with the change so it is never made twice."
            );

            migrationBuilder.CreateTable(
                name: "player_respects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    giver_id = table.Column<int>(type: "integer", nullable: false),
                    receiver_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<byte>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (byte)0
                    ),
                    receiver_total = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_respects", x => x.id);
                },
                comment: "One respect one player gave another: who, when, and whether it went through."
            );

            migrationBuilder.CreateTable(
                name: "player_subscription_grants",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    subscription_type = table.Column<int>(type: "integer", nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    purchased = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_subscription_grants", x => x.id);
                },
                comment: "Every time a player's subscription was extended: how many days, bought or given, and until when."
            );

            migrationBuilder.CreateTable(
                name: "player_wallet_credit_receipts",
                columns: table => new
                {
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_player_wallet_credit_receipts",
                        x => new { x.player_id, x.reference }
                    );
                },
                comment: "Records that a referenced credit was applied, so repeating it changes nothing."
            );

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    motto = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    figure = table.Column<string>(
                        type: "character varying(279)",
                        maxLength: 279,
                        nullable: false,
                        defaultValue: "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64"
                    ),
                    gender = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    respect_points = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    respects_left = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    pet_respects_left = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    respect_replenishes_left = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    respect_reset_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_login_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    last_seen_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                    table.CheckConstraint("ck_players_name_key", "name_key = lower(trim(name))");
                },
                comment: "The hotel's players."
            );

            migrationBuilder.CreateTable(
                name: "quest_campaigns",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    sort_order = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    ends_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quest_campaigns", x => x.id);
                },
                comment: "A quest campaign: the quest window's rows, in SortOrder."
            );

            migrationBuilder.CreateTable(
                name: "quest_daily_task_definitions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    quest_type = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    targets = table.Column<List<string>>(type: "text[]", nullable: false),
                    required_repeats = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    image_version = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    catalog_name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    is_bonus = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    reward_product_type = table.Column<short>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (short)8
                    ),
                    reward_type_id = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    reward_amount = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quest_daily_task_definitions", x => x.id);
                },
                comment: "A daily task a player can be given."
            );

            migrationBuilder.CreateTable(
                name: "room_chat_logs",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    target_player_id = table.Column<int>(type: "integer", nullable: true),
                    message = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    typed_message = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_chat_logs", x => x.id);
                },
                comment: "One line said in a room."
            );

            migrationBuilder.CreateTable(
                name: "room_entry_logs",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_entry_logs", x => x.id);
                },
                comment: "A player entering a room."
            );

            migrationBuilder.CreateTable(
                name: "room_models",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    door_x = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    door_y = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    door_rotation = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    custom = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_models", x => x.id);
                },
                comment: "Room layouts: the standard models rooms are created on, and each room's own saved floor plan."
            );

            migrationBuilder.CreateTable(
                name: "server_setting_changes",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    setting_path = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    before = table.Column<string>(type: "text", nullable: true),
                    after = table.Column<string>(type: "text", nullable: true),
                    secret = table.Column<bool>(type: "boolean", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_setting_changes", x => x.id);
                },
                comment: "A server setting changed in the admin panel, and by whom: the panel's value before and after, as JSON, null where the panel had none."
            );

            migrationBuilder.CreateTable(
                name: "server_settings",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    setting_path = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_settings", x => x.id);
                },
                comment: "A server setting overridden in the admin panel: its configuration path (Turbo:Rooms:MaxUsersPerRoom) and its value as JSON."
            );

            migrationBuilder.CreateTable(
                name: "songs",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    author = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    track = table.Column<string>(type: "text", nullable: false),
                    length_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_official = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_songs", x => x.id);
                },
                comment: "A trax song: the track a song disk plays in a jukebox."
            );

            migrationBuilder.CreateTable(
                name: "wired_chest_transactions",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    player_name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    definition_info = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wired_chest_transactions", x => x.id);
                },
                comment: "One thing that moved furni or credits into or out of wired chests, written in the same database transaction as the move, so the logs never disagree with the chests."
            );

            migrationBuilder.CreateTable(
                name: "asset_published_files",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    path = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    hash = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_published_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_published_files_asset_publish_targets_target_id",
                        column: x => x.target_id,
                        principalTable: "asset_publish_targets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A file a publish target holds, as it was last sent there: a publish sends only what is new or whose hash changed, and one that stops part way resumes from what is recorded."
            );

            migrationBuilder.CreateTable(
                name: "asset_publishes",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    uploaded = table.Column<int>(type: "integer", nullable: false),
                    skipped = table.Column<int>(type: "integer", nullable: false),
                    deleted = table.Column<int>(type: "integer", nullable: false),
                    bytes = table.Column<long>(type: "bigint", nullable: false),
                    error = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    finished_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_publishes", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_publishes_asset_publish_targets_target_id",
                        column: x => x.target_id,
                        principalTable: "asset_publish_targets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A publish, for its target's history: who started it, what it sent and removed, and how it ended."
            );

            migrationBuilder.CreateTable(
                name: "catalog_offers",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    page_id = table.Column<int>(type: "integer", nullable: false),
                    product_code = table.Column<string>(type: "text", nullable: false),
                    cost_credits = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    cost_currency = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    currency_type_id = table.Column<int>(type: "integer", nullable: true),
                    can_gift = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    can_bundle = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    club_level = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    club_gift_days_required = table.Column<int>(type: "integer", nullable: true),
                    visible = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    sort_order = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_offers", x => x.id);
                    table.CheckConstraint(
                        "ck_catalog_offers_costs",
                        "cost_credits >= 0 AND cost_currency >= 0"
                    );
                    table.CheckConstraint(
                        "ck_catalog_offers_currency",
                        "cost_currency = 0 OR currency_type_id IS NOT NULL"
                    );
                    table.ForeignKey(
                        name: "fk_catalog_offers_catalog_currency_types_currency_type_id",
                        column: x => x.currency_type_id,
                        principalTable: "catalog_currency_types",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "fk_catalog_offers_catalog_pages_page_id",
                        column: x => x.page_id,
                        principalTable: "catalog_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "What the catalog sells: an offer on a page, its price, and who may buy or gift it."
            );

            migrationBuilder.CreateTable(
                name: "catalog_page_expiries",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    page_id = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    image = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_page_expiries", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalog_page_expiries_catalog_pages_page_id",
                        column: x => x.page_id,
                        principalTable: "catalog_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A catalog page the reception counts down to: the expiring page widget shows the page whose ExpiresAt comes first among those still to come, with its own texts and teaser picture."
            );

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    credits = table.Column<int>(type: "integer", nullable: false),
                    currency_type_id = table.Column<int>(type: "integer", nullable: true),
                    currency_amount = table.Column<int>(type: "integer", nullable: false),
                    furniture_definition_id = table.Column<int>(type: "integer", nullable: true),
                    furniture_quantity = table.Column<int>(type: "integer", nullable: false),
                    badge_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    max_uses = table.Column<int>(type: "integer", nullable: true),
                    uses = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vouchers", x => x.id);
                    table.CheckConstraint(
                        "ck_vouchers_amounts",
                        "credits >= 0 AND currency_amount >= 0 AND furniture_quantity >= 0 AND (currency_amount = 0 OR currency_type_id IS NOT NULL)"
                    );
                    table.CheckConstraint(
                        "ck_vouchers_uses",
                        "uses >= 0 AND (max_uses IS NULL OR (max_uses >= 1 AND uses <= max_uses))"
                    );
                    table.ForeignKey(
                        name: "fk_vouchers_catalog_currency_types_currency_type_id",
                        column: x => x.currency_type_id,
                        principalTable: "catalog_currency_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "fk_vouchers_furniture_definitions_furniture_definition_id",
                        column: x => x.furniture_definition_id,
                        principalTable: "furniture_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull
                    );
                },
                comment: "A voucher code and what it gives."
            );

            migrationBuilder.CreateTable(
                name: "gamedata_changes",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    change_set_id = table.Column<int>(type: "integer", nullable: false),
                    record_type = table.Column<int>(type: "integer", nullable: false),
                    record_id = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    before = table.Column<string>(type: "text", nullable: true),
                    after = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gamedata_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_gamedata_changes_gamedata_change_sets_change_set_id",
                        column: x => x.change_set_id,
                        principalTable: "gamedata_change_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "One row a change set touched."
            );

            migrationBuilder.CreateTable(
                name: "moderation_cfh_report_chat_lines",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    report_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_cfh_report_chat_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_moderation_cfh_report_chat_lines_moderation_cfh_reports_rep",
                        column: x => x.report_id,
                        principalTable: "moderation_cfh_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A chat line sent with a call for help, in the order the reporter sent them."
            );

            migrationBuilder.CreateTable(
                name: "permission_group_meta",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    meta_key = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    value = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    is_temporary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_group_meta", x => x.id);
                    table.ForeignKey(
                        name: "fk_permission_group_meta_permission_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "permission_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A meta value a group sets (client.security_level = 5)."
            );

            migrationBuilder.CreateTable(
                name: "permission_group_nodes",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    node = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    value = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    is_temporary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_group_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_permission_group_nodes_permission_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "permission_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A node a group grants (Value true) or denies (false)."
            );

            migrationBuilder.CreateTable(
                name: "permission_group_parents",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    parent_group_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_group_parents", x => x.id);
                    table.ForeignKey(
                        name: "fk_permission_group_parents_permission_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "permission_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_permission_group_parents_permission_groups_parent_group_id",
                        column: x => x.parent_group_id,
                        principalTable: "permission_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "One group inheriting from another."
            );

            migrationBuilder.CreateTable(
                name: "auth_admin_passkeys",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    credential_id = table.Column<byte[]>(
                        type: "bytea",
                        maxLength: 1023,
                        nullable: false
                    ),
                    public_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    sign_count = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    aaguid = table.Column<Guid>(type: "uuid", nullable: false),
                    last_used_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_admin_passkeys", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_admin_passkeys_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A passkey a player signs in to the admin panel with: the public key their authenticator made, never anything secret."
            );

            migrationBuilder.CreateTable(
                name: "auth_discord_links",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    discord_id = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    discord_username = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_discord_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_discord_links_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "The Discord account a player signs in to the public site with: one each way, so a Discord account is one player and a player is one Discord account."
            );

            migrationBuilder.CreateTable(
                name: "auth_login_tickets",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    ticket = table.Column<string>(type: "text", nullable: false),
                    reusable = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_login_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_login_tickets_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "The ticket a player's client logs in with, stored as its hash; one per player, created_at is when it was issued."
            );

            migrationBuilder.CreateTable(
                name: "auth_web_sessions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    token_hash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_web_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_web_sessions_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A sign-in to the public site."
            );

            migrationBuilder.CreateTable(
                name: "catalog_bonus_rare_receipts",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    reference = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    campaign_code = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    credits = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_bonus_rare_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalog_bonus_rare_receipts_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Credits bought, recorded once for the bonus rare under the purchase integration's own reference: a reference recorded again counts nothing."
            );

            migrationBuilder.CreateTable(
                name: "hotel_community_goal_contributions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    goal_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    side_one = table.Column<int>(type: "integer", nullable: false),
                    side_two = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    voted_side = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotel_community_goal_contributions", x => x.id);
                    table.ForeignKey(
                        name: "fk_hotel_community_goal_contributions_hotel_community_goals_go",
                        column: x => x.goal_id,
                        principalTable: "hotel_community_goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_hotel_community_goal_contributions_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "What one player has given a community goal: to each side (a goal of one side has only the first), and the side they voted for."
            );

            migrationBuilder.CreateTable(
                name: "messenger_blocks",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    blocked_player_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messenger_blocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_messenger_blocks_players_blocked_player_id",
                        column: x => x.blocked_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_messenger_blocks_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Players a player has blocked from messaging them."
            );

            migrationBuilder.CreateTable(
                name: "messenger_categories",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messenger_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_messenger_categories_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "The categories a player sorts their friend list into."
            );

            migrationBuilder.CreateTable(
                name: "messenger_ignores",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    ignored_player_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messenger_ignores", x => x.id);
                    table.ForeignKey(
                        name: "fk_messenger_ignores_players_ignored_player_id",
                        column: x => x.ignored_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_messenger_ignores_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Players a player has ignored in rooms."
            );

            migrationBuilder.CreateTable(
                name: "messenger_requests",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    requested_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messenger_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_messenger_requests_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_messenger_requests_players_requested_id",
                        column: x => x.requested_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Friend requests waiting for an answer."
            );

            migrationBuilder.CreateTable(
                name: "player_badges",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    badge_code = table.Column<string>(type: "text", nullable: false),
                    slot_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_badges", x => x.id);
                    table.CheckConstraint(
                        "ck_player_badges_slot_id",
                        "slot_id IS NULL OR slot_id > 0"
                    );
                    table.ForeignKey(
                        name: "fk_player_badges_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "The badges each player owns, and which slot each is worn in."
            );

            migrationBuilder.CreateTable(
                name: "player_bonus_rare_progress",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    campaign_code = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    credits_toward_next_reward = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    rewards_received = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_bonus_rare_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_bonus_rare_progress_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A player's progress toward a bonus rare campaign's reward, by the campaign's code."
            );

            migrationBuilder.CreateTable(
                name: "player_bound_clothing",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    definition_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_bound_clothing", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_bound_clothing_furniture_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "furniture_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_player_bound_clothing_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Clothing furni a player has bound: a figure_purchasable_set furni they used, whose figure sets are theirs since."
            );

            migrationBuilder.CreateTable(
                name: "player_chat_styles",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    chat_style_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_chat_styles", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_chat_styles_hotel_chat_styles_chat_style_id",
                        column: x => x.chat_style_id,
                        principalTable: "hotel_chat_styles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_player_chat_styles_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "The chat bubble styles each player owns."
            );

            migrationBuilder.CreateTable(
                name: "player_club_gifts",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    offer_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_club_gifts", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_club_gifts_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "One Habbo Club gift a player has claimed."
            );

            migrationBuilder.CreateTable(
                name: "player_currencies",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    currency_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pending_reward_amount = table.Column<long>(
                        type: "bigint",
                        nullable: false,
                        defaultValue: 0L
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_currencies", x => x.id);
                    table.CheckConstraint("ck_player_currencies_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_player_currencies_catalog_currency_types_currency_type_id",
                        column: x => x.currency_type_id,
                        principalTable: "catalog_currency_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_player_currencies_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Each player's balance in each currency."
            );

            migrationBuilder.CreateTable(
                name: "player_effects",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    effect_id = table.Column<int>(type: "integer", nullable: false),
                    sub_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    inactive_count = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    is_permanent = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_effects", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_effects_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "One avatar effect a player owns: a row per effect id, however many copies."
            );

            migrationBuilder.CreateTable(
                name: "player_figure_sets",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    set_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_figure_sets", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_figure_sets_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A piece of clothing a player owns: a figure set the figure data marks as sold, which the player may wear once they have it."
            );

            migrationBuilder.CreateTable(
                name: "player_navigator_collapsed_categories",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    search_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_navigator_collapsed_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_navigator_collapsed_categories_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Navigator sections each player has collapsed."
            );

            migrationBuilder.CreateTable(
                name: "player_navigator_saved_searches",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    search_id = table.Column<int>(type: "integer", nullable: false),
                    search_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    filter = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_navigator_saved_searches", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_navigator_saved_searches_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Searches each player has saved in the navigator."
            );

            migrationBuilder.CreateTable(
                name: "player_navigator_view_modes",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    search_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    view_mode = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_navigator_view_modes", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_navigator_view_modes_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Whether each player sees a navigator section as a list or as tiles."
            );

            migrationBuilder.CreateTable(
                name: "player_outfits",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    slot_id = table.Column<int>(type: "integer", nullable: false),
                    figure = table.Column<string>(
                        type: "character varying(279)",
                        maxLength: 279,
                        nullable: false
                    ),
                    gender = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_outfits", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_outfits_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Looks each player has saved in their wardrobe."
            );

            migrationBuilder.CreateTable(
                name: "player_permission_groups",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    is_temporary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_permission_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_permission_groups_permission_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "permission_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_player_permission_groups_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A group a player holds directly."
            );

            migrationBuilder.CreateTable(
                name: "player_permission_meta",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    meta_key = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    value = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    is_temporary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_permission_meta", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_permission_meta_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A meta value set on one player, beating anything their groups set."
            );

            migrationBuilder.CreateTable(
                name: "player_permission_nodes",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    node = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    value = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    is_temporary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_permission_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_permission_nodes_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A node set on one player, beating anything their groups say: a grant their groups do not give, or a denial (a sanction) of one they do."
            );

            migrationBuilder.CreateTable(
                name: "player_reward_tracks",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    track_id = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    premium = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    progress_json = table.Column<string>(
                        type: "text",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_reward_tracks", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_reward_tracks_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A player's progress on one reward track (the tracks themselves are configuration)."
            );

            migrationBuilder.CreateTable(
                name: "player_settings",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    generic_volume = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 100
                    ),
                    furni_volume = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 100
                    ),
                    trax_volume = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 100
                    ),
                    room_invites_ignored = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    friend_requests_disabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    profile_hidden = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    room_camera_follow_disabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    ui_flags = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    chat_style_id = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    chat_font_size = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    chat_mode = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    chat_bubble_width = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    chat_scroll_speed = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    online_indicator_preference = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    wired_menu_button = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    wired_inspect_button = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    wired_play_test_mode = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    wired_variable_syntax_mode = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    wired_whisper_disabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    wired_show_all_notifications = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    wired_ui_style = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false,
                        defaultValue: ""
                    ),
                    home_room_id = table.Column<int>(type: "integer", nullable: true),
                    navigator_window_x = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 427
                    ),
                    navigator_window_y = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 41
                    ),
                    navigator_window_width = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 425
                    ),
                    navigator_window_height = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 400
                    ),
                    navigator_left_pane_hidden = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    navigator_results_mode = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_settings_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Each player's client settings: volumes, chat, navigator window and home room."
            );

            migrationBuilder.CreateTable(
                name: "player_subscriptions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    subscription_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    first_subscribed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    total_days_subscribed = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    purchased_days_subscribed = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    furni_limit = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_subscriptions_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Each player's Habbo Club and Builders Club membership."
            );

            migrationBuilder.CreateTable(
                name: "player_unseen_items",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_unseen_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_unseen_items_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Something a player received and has not looked at yet: the client draws it as \"new\" until they open its inventory tab."
            );

            migrationBuilder.CreateTable(
                name: "quests",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    campaign_id = table.Column<int>(type: "integer", nullable: false),
                    localization_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    type = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    target = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    total_steps = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    activity_point_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    reward_amount = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    sort_order = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    image_version = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    catalog_page_name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    chain_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    easy = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quests", x => x.id);
                    table.ForeignKey(
                        name: "fk_quests_quest_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "quest_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "One quest of a campaign, done in SortOrder."
            );

            migrationBuilder.CreateTable(
                name: "player_daily_tasks",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    definition_id = table.Column<int>(type: "integer", nullable: false),
                    day_starts_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    repeats = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    counted = table.Column<List<string>>(type: "text[]", nullable: false),
                    status = table.Column<byte>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (byte)0
                    ),
                    completed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    claimed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_daily_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_daily_tasks_definition_id",
                        column: x => x.definition_id,
                        principalTable: "quest_daily_task_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_player_daily_tasks_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A daily task given to a player for one day."
            );

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(
                        type: "text",
                        nullable: false,
                        defaultValue: ""
                    ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    door_mode = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    password = table.Column<string>(type: "text", nullable: true),
                    model_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    players_max = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 25
                    ),
                    paint_wall = table.Column<string>(type: "text", nullable: true),
                    paint_floor = table.Column<string>(type: "text", nullable: true),
                    paint_landscape = table.Column<string>(type: "text", nullable: true),
                    wall_height = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: -1
                    ),
                    hide_walls = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    thickness_wall = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    thickness_floor = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    allow_walk_through = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    allow_pets = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    allow_pets_eat = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    trade_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    mute_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    kick_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    ban_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chat_flood_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 2
                    ),
                    leave_on_door_tile = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    idle_sleep_enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    idle_sleep_timeout_seconds = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    idle_autokick_enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    idle_autokick_timeout_seconds = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    mute_all_pets = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    staff_pick = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    hidden_by_bc = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    wired_modify_permission_mask = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 2
                    ),
                    wired_read_permission_mask = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 2
                    ),
                    wired_timezone = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false,
                        defaultValue: "UTC"
                    ),
                    last_active_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                    table.ForeignKey(
                        name: "fk_rooms_navigator_room_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "navigator_room_categories",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "fk_rooms_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_rooms_room_models_model_id",
                        column: x => x.model_id,
                        principalTable: "room_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                },
                comment: "The hotel's rooms."
            );

            migrationBuilder.CreateTable(
                name: "wired_chest_transaction_entries",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    transaction_id = table.Column<long>(type: "bigint", nullable: false),
                    chest_item_id = table.Column<int>(type: "integer", nullable: false),
                    is_deposit = table.Column<bool>(type: "boolean", nullable: false),
                    is_coins = table.Column<bool>(type: "boolean", nullable: false),
                    is_wall_item = table.Column<bool>(type: "boolean", nullable: false),
                    sprite_id = table.Column<int>(type: "integer", nullable: false),
                    poster_id = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    count = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wired_chest_transaction_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_wired_chest_transaction_entries_wired_chest_transactions_tr",
                        column: x => x.transaction_id,
                        principalTable: "wired_chest_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "How much of one kind went into or out of one chest in a transaction: credits, or furni of one type."
            );

            migrationBuilder.CreateTable(
                name: "catalog_products",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    offer_id = table.Column<int>(type: "integer", nullable: false),
                    product_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    definition_id = table.Column<int>(type: "integer", nullable: true),
                    extra_param = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    subscription_type = table.Column<int>(type: "integer", nullable: true),
                    subscription_days = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_products", x => x.id);
                    table.CheckConstraint("ck_catalog_products_quantity", "quantity >= 1");
                    table.ForeignKey(
                        name: "fk_catalog_products_catalog_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "catalog_offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_catalog_products_furniture_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "furniture_definitions",
                        principalColumn: "id"
                    );
                },
                comment: "What one catalog offer gives: furni, a badge, an effect, a pet or club time."
            );

            migrationBuilder.CreateTable(
                name: "voucher_redemptions",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    voucher_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_voucher_redemptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_voucher_redemptions_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_voucher_redemptions_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A player's redemption of a voucher: one each, which the unique index holds to."
            );

            migrationBuilder.CreateTable(
                name: "messenger_friends",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    friend_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    relation = table.Column<short>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (short)0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messenger_friends", x => x.id);
                    table.ForeignKey(
                        name: "fk_messenger_friends_messenger_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "messenger_categories",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "fk_messenger_friends_players_friend_id",
                        column: x => x.friend_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_messenger_friends_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Friendships, one row for each side of each friendship."
            );

            migrationBuilder.CreateTable(
                name: "player_quests",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    quest_id = table.Column<int>(type: "integer", nullable: false),
                    accepted = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    completed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_quests", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_quests_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_player_quests_quests_quest_id",
                        column: x => x.quest_id,
                        principalTable: "quests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A player's progress on a quest: accepted (the one they are doing), the steps done, and when it was completed."
            );

            migrationBuilder.CreateTable(
                name: "bots",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    room_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    motto = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    figure = table.Column<string>(
                        type: "character varying(279)",
                        maxLength: 279,
                        nullable: false
                    ),
                    gender = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    x = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    y = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    z = table.Column<double>(
                        type: "double precision",
                        nullable: false,
                        defaultValue: 0.0
                    ),
                    direction = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    free_roam = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    chat_text = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    auto_chat = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    chat_delay_seconds = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    mix_sentences = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    dance_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bots", x => x.id);
                    table.ForeignKey(
                        name: "fk_bots_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_bots_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id"
                    );
                },
                comment: "Bots players own: in their inventory or placed in a room."
            );

            migrationBuilder
                .CreateTable(
                    name: "furniture",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "integer", nullable: false)
                            .Annotation(
                                "Npgsql:ValueGenerationStrategy",
                                NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                            ),
                        player_id = table.Column<int>(type: "integer", nullable: false),
                        definition_id = table.Column<int>(type: "integer", nullable: false),
                        room_id = table.Column<int>(type: "integer", nullable: true),
                        x = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                        y = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                        z = table.Column<double>(
                            type: "double precision",
                            nullable: false,
                            defaultValue: 0.0
                        ),
                        direction = table.Column<int>(
                            type: "integer",
                            nullable: false,
                            defaultValue: 0
                        ),
                        wall_offset = table.Column<int>(
                            type: "integer",
                            nullable: false,
                            defaultValue: 0
                        ),
                        extra_data = table.Column<string>(type: "text", nullable: true),
                        chest_item_id = table.Column<int>(type: "integer", nullable: true),
                        chest_transaction_id = table.Column<long>(type: "bigint", nullable: true),
                        held_position = table.Column<int>(type: "integer", nullable: true),
                        created_at = table.Column<DateTime>(
                            type: "timestamp with time zone",
                            nullable: false,
                            defaultValueSql: "CURRENT_TIMESTAMP"
                        ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("pk_furniture", x => x.id);
                        table.CheckConstraint(
                            "ck_furniture_one_place",
                            "room_id IS NULL OR chest_item_id IS NULL"
                        );
                        table.ForeignKey(
                            name: "fk_furniture_furniture_chest_item_id",
                            column: x => x.chest_item_id,
                            principalTable: "furniture",
                            principalColumn: "id",
                            onDelete: ReferentialAction.SetNull
                        );
                        table.ForeignKey(
                            name: "fk_furniture_furniture_definitions_definition_id",
                            column: x => x.definition_id,
                            principalTable: "furniture_definitions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Restrict
                        );
                        table.ForeignKey(
                            name: "fk_furniture_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "fk_furniture_rooms_room_id",
                            column: x => x.room_id,
                            principalTable: "rooms",
                            principalColumn: "id"
                        );
                    },
                    comment: "Every furni a player owns: in their inventory, placed in a room, or held inside another item."
                )
                .Annotation("Npgsql:StorageParameter:fillfactor", 80);

            migrationBuilder.CreateTable(
                name: "furniture_builders_club",
                columns: table => new
                {
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    room_object_id = table.Column<int>(type: "integer", nullable: false),
                    definition_id = table.Column<int>(type: "integer", nullable: false),
                    placed_by_player_id = table.Column<int>(type: "integer", nullable: false),
                    offer_id = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    y = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    z = table.Column<double>(
                        type: "double precision",
                        nullable: false,
                        defaultValue: 0.0
                    ),
                    direction = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    wall_offset = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    extra_data = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_furniture_builders_club",
                        x => new { x.room_id, x.room_object_id }
                    );
                    table.ForeignKey(
                        name: "fk_furniture_builders_club_furniture_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "furniture_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_furniture_builders_club_players_placed_by_player_id",
                        column: x => x.placed_by_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_furniture_builders_club_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A furni the Builders Club lends."
            );

            migrationBuilder.CreateTable(
                name: "guilds",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: false
                    ),
                    description = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    badge_code = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    primary_color_id = table.Column<int>(type: "integer", nullable: false),
                    secondary_color_id = table.Column<int>(type: "integer", nullable: false),
                    guild_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    rights_level = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guilds", x => x.id);
                    table.ForeignKey(
                        name: "fk_guilds_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_guilds_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A group."
            );

            migrationBuilder.CreateTable(
                name: "pets",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    room_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    palette_id = table.Column<int>(type: "integer", nullable: false),
                    breed_id = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(
                        type: "character varying(6)",
                        maxLength: 6,
                        nullable: false
                    ),
                    custom_parts = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    experience = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    energy = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    nutrition = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 100
                    ),
                    respect = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rarity_level = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    has_saddle = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    anyone_can_ride = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    breeding_permission = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    x = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    y = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    z = table.Column<double>(
                        type: "double precision",
                        nullable: false,
                        defaultValue: 0.0
                    ),
                    direction = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    watered_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    harvested_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    energy_decay_due_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    nutrition_decay_due_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pets", x => x.id);
                    table.ForeignKey(
                        name: "fk_pets_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_pets_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id"
                    );
                },
                comment: "Pets players own: in their inventory or placed in a room."
            );

            migrationBuilder.CreateTable(
                name: "player_favorite_rooms",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_favorite_rooms", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_favorite_rooms_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_player_favorite_rooms_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "The rooms each player has marked as a favourite."
            );

            migrationBuilder.CreateTable(
                name: "room_bans",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_bans", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_bans_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_room_bans_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Players banned from a room, and until when."
            );

            migrationBuilder.CreateTable(
                name: "room_events",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    description = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_events_navigator_event_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "navigator_event_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_room_events_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_room_events_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Events players are running in their rooms, as the navigator lists them."
            );

            migrationBuilder.CreateTable(
                name: "room_filter_words",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    word = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_filter_words", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_filter_words_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Words a room's owner has chosen to filter in their room's chat."
            );

            migrationBuilder.CreateTable(
                name: "room_mutes",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_mutes", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_mutes_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_room_mutes_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Players muted in a room, and until when."
            );

            migrationBuilder.CreateTable(
                name: "room_raid_protections",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    detection_sensitivity = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    action_type = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    ban_duration_seconds = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 900
                    ),
                    guard_enabled = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    guard_duration_seconds = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 900
                    ),
                    guard_sensitivity = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    last_raid_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_raid_protections", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_raid_protections_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A room's raid protection settings."
            );

            migrationBuilder.CreateTable(
                name: "room_ratings",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_ratings", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_ratings_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_room_ratings_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Players' votes on rooms."
            );

            migrationBuilder.CreateTable(
                name: "room_rights",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_rights", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_rights_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_room_rights_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Players a room's owner has given rights in it."
            );

            migrationBuilder.CreateTable(
                name: "catalog_ltd_series",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    catalog_product_id = table.Column<int>(type: "integer", nullable: false),
                    total_quantity = table.Column<int>(type: "integer", nullable: false),
                    remaining_quantity = table.Column<int>(type: "integer", nullable: false),
                    raffle_window_seconds = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 20
                    ),
                    is_active = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    has_raffle_finished = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    starts_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    ends_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_ltd_series", x => x.id);
                    table.CheckConstraint(
                        "ck_catalog_ltd_series_quantity",
                        "remaining_quantity >= 0 AND remaining_quantity <= total_quantity"
                    );
                    table.ForeignKey(
                        name: "fk_catalog_ltd_series_catalog_products_catalog_product_id",
                        column: x => x.catalog_product_id,
                        principalTable: "catalog_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                },
                comment: "Configuration for an LTD (Limited Edition) series."
            );

            migrationBuilder.CreateTable(
                name: "wired_chests",
                columns: table => new
                {
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    coins = table.Column<int>(type: "integer", nullable: false),
                    capacity_level = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wired_chests", x => x.item_id);
                    table.CheckConstraint(
                        "ck_wired_chests_amounts",
                        "coins >= 0 AND capacity_level >= 0"
                    );
                    table.ForeignKey(
                        name: "fk_wired_chests_furniture_item_id",
                        column: x => x.item_id,
                        principalTable: "furniture",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "What a wired chest holds besides its furni rows: the credits in a credit chest and how many times its capacity was bought up."
            );

            migrationBuilder.CreateTable(
                name: "guild_forum_read_markers",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    guild_id = table.Column<int>(type: "integer", nullable: false),
                    last_read_message_id = table.Column<int>(type: "integer", nullable: false),
                    read_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_forum_read_markers", x => x.id);
                    table.ForeignKey(
                        name: "fk_guild_forum_read_markers_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_guild_forum_read_markers_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "How far a player has read a forum: the last message read, numbered within the forum."
            );

            migrationBuilder.CreateTable(
                name: "guild_forum_threads",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    guild_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(
                        type: "character varying(120)",
                        maxLength: 120,
                        nullable: false
                    ),
                    is_sticky = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    is_locked = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    state = table.Column<byte>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (byte)0
                    ),
                    moderator_id = table.Column<int>(type: "integer", nullable: true),
                    moderated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    message_count = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    last_message_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_forum_threads", x => x.id);
                    table.ForeignKey(
                        name: "fk_guild_forum_threads_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A forum thread."
            );

            migrationBuilder.CreateTable(
                name: "guild_forums",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    guild_id = table.Column<int>(type: "integer", nullable: false),
                    read_permission = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    post_message_permission = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    post_thread_permission = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 1
                    ),
                    moderate_permission = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 2
                    ),
                    thread_count = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    message_count = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 0
                    ),
                    last_message_player_id = table.Column<int>(type: "integer", nullable: true),
                    last_message_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_forums", x => x.id);
                    table.ForeignKey(
                        name: "fk_guild_forums_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A group's forum, opened when the group's owner buys a forum terminal for it."
            );

            migrationBuilder.CreateTable(
                name: "guild_members",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    guild_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    member_rank = table.Column<int>(
                        type: "integer",
                        nullable: false,
                        defaultValue: 2
                    ),
                    is_favourite = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    joined_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_guild_members_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_guild_members_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "One player's standing in one group."
            );

            migrationBuilder.CreateTable(
                name: "catalog_ltd_entries",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    series_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result = table.Column<byte>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (byte)0
                    ),
                    serial_number = table.Column<int>(type: "integer", nullable: true),
                    processed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_ltd_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalog_ltd_entries_catalog_ltd_series_series_id",
                        column: x => x.series_id,
                        principalTable: "catalog_ltd_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_catalog_ltd_entries_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "Every claim on an LTD series: raffle entries and sales, with the serial each won."
            );

            migrationBuilder.CreateTable(
                name: "guild_forum_messages",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    guild_id = table.Column<int>(type: "integer", nullable: false),
                    thread_id = table.Column<int>(type: "integer", nullable: false),
                    forum_message_id = table.Column<int>(type: "integer", nullable: false),
                    thread_index = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: false
                    ),
                    state = table.Column<byte>(
                        type: "smallint",
                        nullable: false,
                        defaultValue: (byte)0
                    ),
                    moderator_id = table.Column<int>(type: "integer", nullable: true),
                    moderated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_forum_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_guild_forum_messages_guild_forum_threads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "guild_forum_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                },
                comment: "A forum message."
            );

            migrationBuilder.CreateIndex(
                name: "ix_achievement_audit_logs_operation_id",
                table: "achievement_audit_logs",
                column: "operation_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_achievement_facts_occurred_at",
                table: "achievement_facts",
                column: "occurred_at",
                filter: "processed"
            );

            migrationBuilder.CreateIndex(
                name: "ix_achievement_facts_player_id_id",
                table: "achievement_facts",
                columns: new[] { "player_id", "id" },
                filter: "NOT processed"
            );

            migrationBuilder.CreateIndex(
                name: "ix_achievement_facts_player_id_operation_id",
                table: "achievement_facts",
                columns: new[] { "player_id", "operation_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_achievement_progress_player_id",
                table: "achievement_progress",
                column: "player_id",
                filter: "pending_delivery"
            );

            migrationBuilder.CreateIndex(
                name: "ix_achievement_projections_player_id",
                table: "achievement_projections",
                column: "player_id",
                filter: "publication_pending"
            );

            migrationBuilder.CreateIndex(
                name: "ix_asset_bundles_kind_name",
                table: "asset_bundles",
                columns: new[] { "kind", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_asset_published_files_target_id_path",
                table: "asset_published_files",
                columns: new[] { "target_id", "path" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_asset_publishes_target_id",
                table: "asset_publishes",
                column: "target_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_admin_passkeys_credential_id",
                table: "auth_admin_passkeys",
                column: "credential_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_admin_passkeys_player_id",
                table: "auth_admin_passkeys",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_discord_links_discord_id",
                table: "auth_discord_links",
                column: "discord_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_discord_links_player_id",
                table: "auth_discord_links",
                column: "player_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_login_tickets_player_id",
                table: "auth_login_tickets",
                column: "player_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_login_tickets_ticket",
                table: "auth_login_tickets",
                column: "ticket",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_web_sessions_expires_at",
                table: "auth_web_sessions",
                column: "expires_at"
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_web_sessions_player_id",
                table: "auth_web_sessions",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_auth_web_sessions_token_hash",
                table: "auth_web_sessions",
                column: "token_hash",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_badge_definitions_badge_code",
                table: "badge_definitions",
                column: "badge_code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_bots_player_id_room_id",
                table: "bots",
                columns: new[] { "player_id", "room_id" }
            );

            migrationBuilder.CreateIndex(name: "ix_bots_room_id", table: "bots", column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_bonus_rare_campaigns_code",
                table: "catalog_bonus_rare_campaigns",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_bonus_rare_campaigns_starts_at",
                table: "catalog_bonus_rare_campaigns",
                column: "starts_at"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_bonus_rare_receipts_player_id",
                table: "catalog_bonus_rare_receipts",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_bonus_rare_receipts_reference",
                table: "catalog_bonus_rare_receipts",
                column: "reference",
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_catalog_currency_types_type_activity_point_type",
                    table: "catalog_currency_types",
                    columns: new[] { "type", "activity_point_type" },
                    unique: true
                )
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_ltd_entries_batch_id_player_id",
                table: "catalog_ltd_entries",
                columns: new[] { "batch_id", "player_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_ltd_entries_player_id",
                table: "catalog_ltd_entries",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_ltd_entries_series_id_player_id_result",
                table: "catalog_ltd_entries",
                columns: new[] { "series_id", "player_id", "result" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_ltd_entries_series_id_serial_number",
                table: "catalog_ltd_entries",
                columns: new[] { "series_id", "serial_number" },
                unique: true,
                filter: "serial_number IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_ltd_series_catalog_product_id",
                table: "catalog_ltd_series",
                column: "catalog_product_id",
                unique: true,
                filter: "is_active"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_offers_currency_type_id",
                table: "catalog_offers",
                column: "currency_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_offers_page_id",
                table: "catalog_offers",
                column: "page_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_page_expiries_expires_at",
                table: "catalog_page_expiries",
                column: "expires_at"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_page_expiries_page_id",
                table: "catalog_page_expiries",
                column: "page_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_pages_parent_id",
                table: "catalog_pages",
                column: "parent_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_products_definition_id",
                table: "catalog_products",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_catalog_products_offer_id",
                table: "catalog_products",
                column: "offer_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_chest_item_id",
                table: "furniture",
                column: "chest_item_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_definition_id",
                table: "furniture",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_player_id_room_id",
                table: "furniture",
                columns: new[] { "player_id", "room_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_room_id",
                table: "furniture",
                column: "room_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_builders_club_definition_id",
                table: "furniture_builders_club",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_builders_club_offer_id",
                table: "furniture_builders_club",
                column: "offer_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_builders_club_placed_by_player_id",
                table: "furniture_builders_club",
                column: "placed_by_player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_definitions_sprite_id_type_category",
                table: "furniture_definitions",
                columns: new[] { "sprite_id", "type", "category" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_definitions_type_name",
                table: "furniture_definitions",
                columns: new[] { "type", "name" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_furniture_transfers_created_at",
                    table: "furniture_transfers",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_furniture_transfers_from_player_id_id",
                table: "furniture_transfers",
                columns: new[] { "from_player_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_transfers_furniture_id_id",
                table: "furniture_transfers",
                columns: new[] { "furniture_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_furniture_transfers_to_player_id_id",
                table: "furniture_transfers",
                columns: new[] { "to_player_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_gamedata_builds_file_hash",
                table: "gamedata_builds",
                columns: new[] { "file", "hash" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_gamedata_changes_change_set_id",
                table: "gamedata_changes",
                column: "change_set_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_gamedata_figures_kind_record_group",
                table: "gamedata_figures",
                columns: new[] { "kind", "record_group" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_gamedata_figures_kind_record_key",
                table: "gamedata_figures",
                columns: new[] { "kind", "record_key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_gamedata_products_code",
                table: "gamedata_products",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_gamedata_texts_text_key",
                table: "gamedata_texts",
                column: "text_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_gamedata_variables_variable_key",
                table: "gamedata_variables",
                column: "variable_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_badge_parts_part_type_part_id",
                table: "guild_badge_parts",
                columns: new[] { "part_type", "part_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_colors_slot_color_id",
                table: "guild_colors",
                columns: new[] { "slot", "color_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forum_messages_guild_id_created_at",
                table: "guild_forum_messages",
                columns: new[] { "guild_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forum_messages_guild_id_forum_message_id",
                table: "guild_forum_messages",
                columns: new[] { "guild_id", "forum_message_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forum_messages_guild_id_player_id",
                table: "guild_forum_messages",
                columns: new[] { "guild_id", "player_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forum_messages_thread_id_thread_index",
                table: "guild_forum_messages",
                columns: new[] { "thread_id", "thread_index" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forum_read_markers_guild_id_read_at",
                table: "guild_forum_read_markers",
                columns: new[] { "guild_id", "read_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forum_read_markers_player_id_guild_id",
                table: "guild_forum_read_markers",
                columns: new[] { "player_id", "guild_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forum_threads_guild_id_is_sticky_last_message_at",
                table: "guild_forum_threads",
                columns: new[] { "guild_id", "is_sticky", "last_message_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_forums_guild_id",
                table: "guild_forums",
                column: "guild_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_members_guild_id_member_rank",
                table: "guild_members",
                columns: new[] { "guild_id", "member_rank" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_members_guild_id_player_id",
                table: "guild_members",
                columns: new[] { "guild_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_members_player_id",
                table: "guild_members",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_guild_members_player_id1",
                table: "guild_members",
                column: "player_id",
                unique: true,
                filter: "is_favourite"
            );

            migrationBuilder.CreateIndex(
                name: "ix_guilds_player_id",
                table: "guilds",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_guilds_room_id",
                table: "guilds",
                column: "room_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_figure_versions_domain_hash",
                table: "habbo_figure_versions",
                columns: new[] { "domain", "hash" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_figures_kind_record_key",
                table: "habbo_figures",
                columns: new[] { "kind", "record_key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_furniture_type_class_name",
                table: "habbo_furniture",
                columns: new[] { "type", "class_name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_furniture_assets_asset_name_revision",
                table: "habbo_furniture_assets",
                columns: new[] { "asset_name", "revision" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_furniture_versions_domain_furniture_data_hash",
                table: "habbo_furniture_versions",
                columns: new[] { "domain", "furniture_data_hash" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_product_versions_domain_hash",
                table: "habbo_product_versions",
                columns: new[] { "domain", "hash" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_products_code",
                table: "habbo_products",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_text_versions_domain_hash",
                table: "habbo_text_versions",
                columns: new[] { "domain", "hash" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_habbo_texts_text_key",
                table: "habbo_texts",
                column: "text_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_hotel_community_goal_contributions_goal_id_player_id",
                table: "hotel_community_goal_contributions",
                columns: new[] { "goal_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_hotel_community_goal_contributions_goal_id_score",
                table: "hotel_community_goal_contributions",
                columns: new[] { "goal_id", "score" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_hotel_community_goal_contributions_player_id",
                table: "hotel_community_goal_contributions",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_hotel_community_goals_code",
                table: "hotel_community_goals",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_hotel_community_goals_starts_at",
                table: "hotel_community_goals",
                column: "starts_at"
            );

            migrationBuilder.CreateIndex(
                name: "ix_hotel_promo_articles_sort_order",
                table: "hotel_promo_articles",
                column: "sort_order"
            );

            migrationBuilder.CreateIndex(
                name: "ix_hotel_settings_key",
                table: "hotel_settings",
                column: "key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_blocks_blocked_player_id",
                table: "messenger_blocks",
                column: "blocked_player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_blocks_player_id_blocked_player_id",
                table: "messenger_blocks",
                columns: new[] { "player_id", "blocked_player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_categories_player_id_name",
                table: "messenger_categories",
                columns: new[] { "player_id", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_friends_category_id",
                table: "messenger_friends",
                column: "category_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_friends_friend_id",
                table: "messenger_friends",
                column: "friend_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_friends_player_id_friend_id",
                table: "messenger_friends",
                columns: new[] { "player_id", "friend_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_ignores_ignored_player_id",
                table: "messenger_ignores",
                column: "ignored_player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_ignores_player_id_ignored_player_id",
                table: "messenger_ignores",
                columns: new[] { "player_id", "ignored_player_id" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_messenger_messages_created_at",
                    table: "messenger_messages",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_messenger_messages_receiver_id_created_at",
                table: "messenger_messages",
                columns: new[] { "receiver_id", "created_at" },
                filter: "NOT delivered"
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_messages_receiver_id_sender_id_created_at",
                table: "messenger_messages",
                columns: new[] { "receiver_id", "sender_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_messages_sender_id_receiver_id_created_at",
                table: "messenger_messages",
                columns: new[] { "sender_id", "receiver_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_requests_player_id_requested_id",
                table: "messenger_requests",
                columns: new[] { "player_id", "requested_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_messenger_requests_requested_id",
                table: "messenger_requests",
                column: "requested_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_action_id",
                table: "moderation_actions",
                columns: new[] { "action", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_actor_id_id",
                table: "moderation_actions",
                columns: new[] { "actor_id", "id" }
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_moderation_actions_created_at",
                    table: "moderation_actions",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_room_id_id",
                table: "moderation_actions",
                columns: new[] { "room_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_target_player_id_id",
                table: "moderation_actions",
                columns: new[] { "target_player_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cfh_report_chat_lines_report_id_position",
                table: "moderation_cfh_report_chat_lines",
                columns: new[] { "report_id", "position" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cfh_reports_reporter_id_closed_at",
                table: "moderation_cfh_reports",
                columns: new[] { "reporter_id", "closed_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_command_logs_command_id",
                table: "moderation_command_logs",
                columns: new[] { "command", "id" }
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_moderation_command_logs_created_at",
                    table: "moderation_command_logs",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_command_logs_outcome_id",
                table: "moderation_command_logs",
                columns: new[] { "outcome", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_command_logs_player_id_id",
                table: "moderation_command_logs",
                columns: new[] { "player_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_moderation_sanctions_player_id_kind",
                table: "moderation_sanctions",
                columns: new[] { "player_id", "kind" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_navigator_room_categories_name",
                table: "navigator_room_categories",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_navigator_top_level_contexts_search_code",
                table: "navigator_top_level_contexts",
                column: "search_code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_permission_audit_logs_target_type_target_id_created_at",
                table: "permission_audit_logs",
                columns: new[] { "target_type", "target_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_permission_group_meta_group_id_meta_key_is_temporary",
                table: "permission_group_meta",
                columns: new[] { "group_id", "meta_key", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_permission_group_nodes_group_id_node_is_temporary",
                table: "permission_group_nodes",
                columns: new[] { "group_id", "node", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_permission_group_parents_group_id_parent_group_id",
                table: "permission_group_parents",
                columns: new[] { "group_id", "parent_group_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_permission_group_parents_parent_group_id",
                table: "permission_group_parents",
                column: "parent_group_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_permission_groups_name",
                table: "permission_groups",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_pet_breeds_type_id_palette_id",
                table: "pet_breeds",
                columns: new[] { "type_id", "palette_id" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_pet_feedings_created_at",
                    table: "pet_feedings",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_pet_feedings_pet_id",
                table: "pet_feedings",
                column: "pet_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_pet_respects_id",
                table: "pet_respects",
                column: "id",
                filter: "status = 0"
            );

            migrationBuilder.CreateIndex(
                name: "ix_pet_respects_pet_id",
                table: "pet_respects",
                column: "pet_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_pet_speech_lines_type_id",
                table: "pet_speech_lines",
                column: "type_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_pets_player_id_room_id",
                table: "pets",
                columns: new[] { "player_id", "room_id" }
            );

            migrationBuilder.CreateIndex(name: "ix_pets_room_id", table: "pets", column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_player_badges_badge_code",
                table: "player_badges",
                column: "badge_code"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_badges_player_id_badge_code",
                table: "player_badges",
                columns: new[] { "player_id", "badge_code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_bonus_rare_progress_player_id_campaign_code",
                table: "player_bonus_rare_progress",
                columns: new[] { "player_id", "campaign_code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_bound_clothing_definition_id",
                table: "player_bound_clothing",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_bound_clothing_player_id_definition_id",
                table: "player_bound_clothing",
                columns: new[] { "player_id", "definition_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_chat_styles_chat_style_id",
                table: "player_chat_styles",
                column: "chat_style_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_chat_styles_player_id_chat_style_id",
                table: "player_chat_styles",
                columns: new[] { "player_id", "chat_style_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_club_gifts_player_id",
                table: "player_club_gifts",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_currencies_currency_type_id",
                table: "player_currencies",
                column: "currency_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_currencies_player_id_currency_type_id",
                table: "player_currencies",
                columns: new[] { "player_id", "currency_type_id" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_player_currency_ledger_created_at",
                    table: "player_currency_ledger",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_player_currency_ledger_player_id_id",
                table: "player_currency_ledger",
                columns: new[] { "player_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_daily_tasks_definition_id",
                table: "player_daily_tasks",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_daily_tasks_player_id_day_starts_at_definition_id",
                table: "player_daily_tasks",
                columns: new[] { "player_id", "day_starts_at", "definition_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_effects_player_id_effect_id",
                table: "player_effects",
                columns: new[] { "player_id", "effect_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_favorite_rooms_player_id_room_id",
                table: "player_favorite_rooms",
                columns: new[] { "player_id", "room_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_favorite_rooms_room_id",
                table: "player_favorite_rooms",
                column: "room_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_figure_sets_player_id_set_id",
                table: "player_figure_sets",
                columns: new[] { "player_id", "set_id" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_player_logins_created_at",
                    table: "player_logins",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_player_logins_ip_address",
                table: "player_logins",
                column: "ip_address"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_logins_machine_id",
                table: "player_logins",
                column: "machine_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_logins_player_id_id",
                table: "player_logins",
                columns: new[] { "player_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_navigator_collapsed_categories_player_id_search_code",
                table: "player_navigator_collapsed_categories",
                columns: new[] { "player_id", "search_code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_navigator_saved_searches_player_id_search_id",
                table: "player_navigator_saved_searches",
                columns: new[] { "player_id", "search_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_navigator_view_modes_player_id_search_code",
                table: "player_navigator_view_modes",
                columns: new[] { "player_id", "search_code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_outfits_player_id_slot_id",
                table: "player_outfits",
                columns: new[] { "player_id", "slot_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_permission_groups_group_id",
                table: "player_permission_groups",
                column: "group_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_permission_groups_player_id_group_id_is_temporary",
                table: "player_permission_groups",
                columns: new[] { "player_id", "group_id", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_permission_meta_player_id_meta_key_is_temporary",
                table: "player_permission_meta",
                columns: new[] { "player_id", "meta_key", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_permission_nodes_player_id_node_is_temporary",
                table: "player_permission_nodes",
                columns: new[] { "player_id", "node", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_quests_player_id",
                table: "player_quests",
                column: "player_id",
                unique: true,
                filter: "accepted"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_quests_player_id_quest_id",
                table: "player_quests",
                columns: new[] { "player_id", "quest_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_quests_quest_id",
                table: "player_quests",
                column: "quest_id"
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_player_respect_changes_created_at",
                    table: "player_respect_changes",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_player_respect_changes_player_id",
                table: "player_respect_changes",
                column: "player_id"
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_player_respects_created_at",
                    table: "player_respects",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_player_respects_giver_id_created_at",
                table: "player_respects",
                columns: new[] { "giver_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_respects_id",
                table: "player_respects",
                column: "id",
                filter: "status = 0"
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_respects_receiver_id_created_at",
                table: "player_respects",
                columns: new[] { "receiver_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_reward_tracks_player_id_track_id",
                table: "player_reward_tracks",
                columns: new[] { "player_id", "track_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_settings_player_id",
                table: "player_settings",
                column: "player_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_subscription_grants_player_id_created_at",
                table: "player_subscription_grants",
                columns: new[] { "player_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_subscriptions_player_id_subscription_type",
                table: "player_subscriptions",
                columns: new[] { "player_id", "subscription_type" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_subscriptions_subscription_type_expires_at",
                table: "player_subscriptions",
                columns: new[] { "subscription_type", "expires_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_player_unseen_items_player_id_category_item_id",
                table: "player_unseen_items",
                columns: new[] { "player_id", "category", "item_id" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_player_wallet_credit_receipts_created_at",
                    table: "player_wallet_credit_receipts",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_players_last_login_at",
                table: "players",
                column: "last_login_at"
            );

            migrationBuilder.CreateIndex(
                name: "ix_players_name_key",
                table: "players",
                column: "name_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_campaigns_code",
                table: "quest_campaigns",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_daily_task_definitions_code",
                table: "quest_daily_task_definitions",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_quests_campaign_id_localization_code",
                table: "quests",
                columns: new[] { "campaign_id", "localization_code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_bans_player_id",
                table: "room_bans",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_bans_room_id_player_id",
                table: "room_bans",
                columns: new[] { "room_id", "player_id" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_room_chat_logs_created_at",
                    table: "room_chat_logs",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_room_chat_logs_player_id_id",
                table: "room_chat_logs",
                columns: new[] { "player_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_chat_logs_room_id_id",
                table: "room_chat_logs",
                columns: new[] { "room_id", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_chat_logs_target_player_id_id",
                table: "room_chat_logs",
                columns: new[] { "target_player_id", "id" }
            );

            migrationBuilder
                .CreateIndex(
                    name: "ix_room_entry_logs_created_at",
                    table: "room_entry_logs",
                    column: "created_at"
                )
                .Annotation("Npgsql:IndexMethod", "brin")
                .Annotation("Npgsql:StorageParameter:autosummarize", true);

            migrationBuilder.CreateIndex(
                name: "ix_room_entry_logs_player_id_created_at",
                table: "room_entry_logs",
                columns: new[] { "player_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_entry_logs_player_id_room_id_created_at",
                table: "room_entry_logs",
                columns: new[] { "player_id", "room_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_entry_logs_room_id_created_at",
                table: "room_entry_logs",
                columns: new[] { "room_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_events_category_id",
                table: "room_events",
                column: "category_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_events_expires_at",
                table: "room_events",
                column: "expires_at"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_events_player_id",
                table: "room_events",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_events_room_id",
                table: "room_events",
                column: "room_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_filter_words_room_id_word",
                table: "room_filter_words",
                columns: new[] { "room_id", "word" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_models_name",
                table: "room_models",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_mutes_player_id",
                table: "room_mutes",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_mutes_room_id_player_id",
                table: "room_mutes",
                columns: new[] { "room_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_raid_protections_room_id",
                table: "room_raid_protections",
                column: "room_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_ratings_player_id",
                table: "room_ratings",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_ratings_room_id_player_id",
                table: "room_ratings",
                columns: new[] { "room_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_rights_player_id",
                table: "room_rights",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_rights_room_id_player_id",
                table: "room_rights",
                columns: new[] { "room_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_rooms_category_id_score",
                table: "rooms",
                columns: new[] { "category_id", "score" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_rooms_hidden_by_bc",
                table: "rooms",
                column: "hidden_by_bc"
            );

            migrationBuilder.CreateIndex(
                name: "ix_rooms_model_id",
                table: "rooms",
                column: "model_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_rooms_player_id",
                table: "rooms",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(name: "ix_rooms_score", table: "rooms", column: "score");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_staff_pick_score",
                table: "rooms",
                columns: new[] { "staff_pick", "score" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_server_settings_setting_path",
                table: "server_settings",
                column: "setting_path",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_voucher_redemptions_player_id",
                table: "voucher_redemptions",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_voucher_redemptions_voucher_id_player_id",
                table: "voucher_redemptions",
                columns: new[] { "voucher_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_code",
                table: "vouchers",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_currency_type_id",
                table: "vouchers",
                column: "currency_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_furniture_definition_id",
                table: "vouchers",
                column: "furniture_definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_wired_chest_transaction_entries_chest_item_id_transaction_id",
                table: "wired_chest_transaction_entries",
                columns: new[] { "chest_item_id", "transaction_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_wired_chest_transaction_entries_transaction_id",
                table: "wired_chest_transaction_entries",
                column: "transaction_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_wired_chest_transactions_room_id_id",
                table: "wired_chest_transactions",
                columns: new[] { "room_id", "id" }
            );

            // Searches by any part of a text, which no b-tree can serve: trigram indexes on the
            // lowered text, matching the queries' lower(column) LIKE '%...%'. Moderators search
            // chat; players search room names in the navigator.
            migrationBuilder.Sql(
                "CREATE INDEX ix_room_chat_logs_message_trgm ON room_chat_logs USING gin (lower(message) gin_trgm_ops);"
            );
            // And by what was typed, for the few lines the word filter changed.
            migrationBuilder.Sql(
                "CREATE INDEX ix_room_chat_logs_typed_message_trgm ON room_chat_logs USING gin (lower(typed_message) gin_trgm_ops) WHERE typed_message IS NOT NULL;"
            );
            migrationBuilder.Sql(
                "CREATE INDEX ix_rooms_name_trgm ON rooms USING gin (lower(name) gin_trgm_ops);"
            );

            // Staff search players and groups by any part of a name.
            migrationBuilder.Sql(
                "CREATE INDEX ix_players_name_key_trgm ON players USING gin (name_key gin_trgm_ops);"
            );
            migrationBuilder.Sql(
                "CREATE INDEX ix_guilds_name_trgm ON guilds USING gin (lower(name) gin_trgm_ops);"
            );

            // Names the game matches without regard to case are unique without regard to case:
            // a filter word, an event category, a song, a currency.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_moderation_filter_words_word ON moderation_filter_words (lower(word));"
            );
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_navigator_event_categories_name ON navigator_event_categories (lower(name));"
            );
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_songs_code ON songs (lower(code));");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_catalog_currency_types_name ON catalog_currency_types (lower(name));"
            );

            // updated_at is restamped on every update by a trigger, so a bulk ExecuteUpdate, which
            // no SaveChanges hook sees, moves it too.
            migrationBuilder.Sql(
                @"CREATE FUNCTION set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at = CURRENT_TIMESTAMP;
  RETURN NEW;
END $$;"
            );

            foreach (var table in new[] { "player_subscriptions" })
                migrationBuilder.Sql(
                    $"CREATE TRIGGER {table}_set_updated_at BEFORE UPDATE ON {table} "
                        + "FOR EACH ROW EXECUTE FUNCTION set_updated_at();"
                );

            // A row's place among its holder's contents, and the chest transaction that put it
            // there, mean nothing once it leaves the holder, however it leaves: a release, or
            // the holder's row going, which clears chest_item_id through its foreign key and
            // reaches no code. The trigger clears them as it leaves; the check keeps them so.
            migrationBuilder.Sql(
                @"CREATE FUNCTION furniture_leave_holder() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  NEW.held_position = NULL;
  NEW.chest_transaction_id = NULL;
  RETURN NEW;
END $$;"
            );
            migrationBuilder.Sql(
                "CREATE TRIGGER furniture_leave_holder BEFORE UPDATE OF chest_item_id ON furniture "
                    + "FOR EACH ROW WHEN (NEW.chest_item_id IS NULL) "
                    + "EXECUTE FUNCTION furniture_leave_holder();"
            );
            migrationBuilder.Sql(
                "ALTER TABLE furniture ADD CONSTRAINT ck_furniture_held "
                    + "CHECK (chest_item_id IS NOT NULL OR (held_position IS NULL AND chest_transaction_id IS NULL));"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "achievement_audit_logs");

            migrationBuilder.DropTable(name: "achievement_definitions");

            migrationBuilder.DropTable(name: "achievement_distinct_values");

            migrationBuilder.DropTable(name: "achievement_facts");

            migrationBuilder.DropTable(name: "achievement_progress");

            migrationBuilder.DropTable(name: "achievement_projections");

            migrationBuilder.DropTable(name: "achievement_wallet_receipts");

            migrationBuilder.DropTable(name: "asset_bundles");

            migrationBuilder.DropTable(name: "asset_published_files");

            migrationBuilder.DropTable(name: "asset_publishes");

            migrationBuilder.DropTable(name: "auth_admin_passkeys");

            migrationBuilder.DropTable(name: "auth_discord_links");

            migrationBuilder.DropTable(name: "auth_login_tickets");

            migrationBuilder.DropTable(name: "auth_web_sessions");

            migrationBuilder.DropTable(name: "badge_definitions");

            migrationBuilder.DropTable(name: "bots");

            migrationBuilder.DropTable(name: "catalog_backups");

            migrationBuilder.DropTable(name: "catalog_bonus_rare_campaigns");

            migrationBuilder.DropTable(name: "catalog_bonus_rare_receipts");

            migrationBuilder.DropTable(name: "catalog_featured_items");

            migrationBuilder.DropTable(name: "catalog_ltd_entries");

            migrationBuilder.DropTable(name: "catalog_page_expiries");

            migrationBuilder.DropTable(name: "furniture_builders_club");

            migrationBuilder.DropTable(name: "furniture_transfers");

            migrationBuilder.DropTable(name: "gamedata_builds");

            migrationBuilder.DropTable(name: "gamedata_changes");

            migrationBuilder.DropTable(name: "gamedata_figures");

            migrationBuilder.DropTable(name: "gamedata_products");

            migrationBuilder.DropTable(name: "gamedata_texts");

            migrationBuilder.DropTable(name: "gamedata_variables");

            migrationBuilder.DropTable(name: "guild_badge_parts");

            migrationBuilder.DropTable(name: "guild_colors");

            migrationBuilder.DropTable(name: "guild_forum_messages");

            migrationBuilder.DropTable(name: "guild_forum_read_markers");

            migrationBuilder.DropTable(name: "guild_forums");

            migrationBuilder.DropTable(name: "guild_members");

            migrationBuilder.DropTable(name: "habbo_figure_versions");

            migrationBuilder.DropTable(name: "habbo_figures");

            migrationBuilder.DropTable(name: "habbo_furniture");

            migrationBuilder.DropTable(name: "habbo_furniture_assets");

            migrationBuilder.DropTable(name: "habbo_furniture_versions");

            migrationBuilder.DropTable(name: "habbo_product_versions");

            migrationBuilder.DropTable(name: "habbo_products");

            migrationBuilder.DropTable(name: "habbo_text_versions");

            migrationBuilder.DropTable(name: "habbo_texts");

            migrationBuilder.DropTable(name: "hotel_community_goal_contributions");

            migrationBuilder.DropTable(name: "hotel_promo_articles");

            migrationBuilder.DropTable(name: "hotel_settings");

            migrationBuilder.DropTable(name: "messenger_blocks");

            migrationBuilder.DropTable(name: "messenger_friends");

            migrationBuilder.DropTable(name: "messenger_ignores");

            migrationBuilder.DropTable(name: "messenger_messages");

            migrationBuilder.DropTable(name: "messenger_requests");

            migrationBuilder.DropTable(name: "moderation_actions");

            migrationBuilder.DropTable(name: "moderation_cfh_report_chat_lines");

            migrationBuilder.DropTable(name: "moderation_cfh_topics");

            migrationBuilder.DropTable(name: "moderation_command_logs");

            migrationBuilder.DropTable(name: "moderation_filter_words");

            migrationBuilder.DropTable(name: "moderation_sanctions");

            migrationBuilder.DropTable(name: "navigator_top_level_contexts");

            migrationBuilder.DropTable(name: "permission_audit_logs");

            migrationBuilder.DropTable(name: "permission_group_meta");

            migrationBuilder.DropTable(name: "permission_group_nodes");

            migrationBuilder.DropTable(name: "permission_group_parents");

            migrationBuilder.DropTable(name: "pet_breeds");

            migrationBuilder.DropTable(name: "pet_feedings");

            migrationBuilder.DropTable(name: "pet_respects");

            migrationBuilder.DropTable(name: "pet_speech_lines");

            migrationBuilder.DropTable(name: "pets");

            migrationBuilder.DropTable(name: "player_badges");

            migrationBuilder.DropTable(name: "player_bonus_rare_progress");

            migrationBuilder.DropTable(name: "player_bound_clothing");

            migrationBuilder.DropTable(name: "player_chat_styles");

            migrationBuilder.DropTable(name: "player_club_gifts");

            migrationBuilder.DropTable(name: "player_currencies");

            migrationBuilder.DropTable(name: "player_currency_ledger");

            migrationBuilder.DropTable(name: "player_daily_tasks");

            migrationBuilder.DropTable(name: "player_effects");

            migrationBuilder.DropTable(name: "player_favorite_rooms");

            migrationBuilder.DropTable(name: "player_figure_sets");

            migrationBuilder.DropTable(name: "player_logins");

            migrationBuilder.DropTable(name: "player_navigator_collapsed_categories");

            migrationBuilder.DropTable(name: "player_navigator_saved_searches");

            migrationBuilder.DropTable(name: "player_navigator_view_modes");

            migrationBuilder.DropTable(name: "player_outfits");

            migrationBuilder.DropTable(name: "player_permission_groups");

            migrationBuilder.DropTable(name: "player_permission_meta");

            migrationBuilder.DropTable(name: "player_permission_nodes");

            migrationBuilder.DropTable(name: "player_quests");

            migrationBuilder.DropTable(name: "player_respect_changes");

            migrationBuilder.DropTable(name: "player_respects");

            migrationBuilder.DropTable(name: "player_reward_tracks");

            migrationBuilder.DropTable(name: "player_settings");

            migrationBuilder.DropTable(name: "player_subscription_grants");

            migrationBuilder.DropTable(name: "player_subscriptions");

            migrationBuilder.DropTable(name: "player_unseen_items");

            migrationBuilder.DropTable(name: "player_wallet_credit_receipts");

            migrationBuilder.DropTable(name: "room_bans");

            migrationBuilder.DropTable(name: "room_chat_logs");

            migrationBuilder.DropTable(name: "room_entry_logs");

            migrationBuilder.DropTable(name: "room_events");

            migrationBuilder.DropTable(name: "room_filter_words");

            migrationBuilder.DropTable(name: "room_mutes");

            migrationBuilder.DropTable(name: "room_raid_protections");

            migrationBuilder.DropTable(name: "room_ratings");

            migrationBuilder.DropTable(name: "room_rights");

            migrationBuilder.DropTable(name: "server_setting_changes");

            migrationBuilder.DropTable(name: "server_settings");

            migrationBuilder.DropTable(name: "songs");

            migrationBuilder.DropTable(name: "voucher_redemptions");

            migrationBuilder.DropTable(name: "wired_chest_transaction_entries");

            migrationBuilder.DropTable(name: "wired_chests");

            migrationBuilder.DropTable(name: "asset_publish_targets");

            migrationBuilder.DropTable(name: "catalog_ltd_series");

            migrationBuilder.DropTable(name: "gamedata_change_sets");

            migrationBuilder.DropTable(name: "guild_forum_threads");

            migrationBuilder.DropTable(name: "hotel_community_goals");

            migrationBuilder.DropTable(name: "messenger_categories");

            migrationBuilder.DropTable(name: "moderation_cfh_reports");

            migrationBuilder.DropTable(name: "hotel_chat_styles");

            migrationBuilder.DropTable(name: "quest_daily_task_definitions");

            migrationBuilder.DropTable(name: "permission_groups");

            migrationBuilder.DropTable(name: "quests");

            migrationBuilder.DropTable(name: "navigator_event_categories");

            migrationBuilder.DropTable(name: "vouchers");

            migrationBuilder.DropTable(name: "wired_chest_transactions");

            migrationBuilder.DropTable(name: "furniture");

            migrationBuilder.DropTable(name: "catalog_products");

            migrationBuilder.DropTable(name: "guilds");

            migrationBuilder.DropTable(name: "quest_campaigns");

            migrationBuilder.DropTable(name: "catalog_offers");

            migrationBuilder.DropTable(name: "furniture_definitions");

            migrationBuilder.DropTable(name: "rooms");

            migrationBuilder.DropTable(name: "catalog_currency_types");

            migrationBuilder.DropTable(name: "catalog_pages");

            migrationBuilder.DropTable(name: "navigator_room_categories");

            migrationBuilder.DropTable(name: "players");

            migrationBuilder.DropTable(name: "room_models");
        }
    }
}
