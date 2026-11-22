using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Migrations;
using Turbo.Import.Morningstar;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Import.MorningstarImportPlayersTests;

namespace Turbo.Tests.Import;

/// <summary>
/// An import from a real MySQL or MariaDB Morningstar database (its ENUM('0','1') flags,
/// TINYINT(1) and DOUBLE columns, as MySqlConnector reads them) into a Turbo database built by
/// the real migrations, with their foreign keys and seeds. Skipped unless <c>TURBO_TEST_MYSQL</c>
/// names a server that may create databases; each test makes and drops two of its own.
/// </summary>
public sealed class MySqlMorningstarImportTests : IAsyncLifetime
{
    private const int ITEMS = 20_000;

    private const string WALK_ON = """{"itemIds":[1]}""";

    private const string DIGITS =
        "(SELECT 0 d UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 "
        + "UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9)";

    private static readonly string? SERVER = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");

    private readonly string _source = "ms_test_" + Guid.NewGuid().ToString("N")[..12];
    private readonly string _target = "turbo_test_" + Guid.NewGuid().ToString("N")[..12];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Connection(string database) =>
        new MySqlConnectionStringBuilder(SERVER!) { Database = database }.ConnectionString;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (SERVER is null)
            return;

        await ExecuteAsync(
            null,
            $"DROP DATABASE IF EXISTS `{_source}`; DROP DATABASE IF EXISTS `{_target}`;"
        );
    }

    private static async Task ExecuteAsync(string? database, string sql)
    {
        var builder = new MySqlConnectionStringBuilder(SERVER!) { AllowUserVariables = true };

        if (database is not null)
            builder.Database = database;

        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 0;
        await command.ExecuteNonQueryAsync();
    }

    private TurboDbContext Context() =>
        new(
            new DbContextOptionsBuilder<TurboDbContext>()
                .UseMySql(Connection(_target), ServerVersion.AutoDetect(SERVER!), mysql =>
                    mysql.MigrationsAssembly("Turbo.Database")
                )
                .Options
        );

    // Morningstar's own column types, as its sqlupdates and Java read them.
    private const string SCHEMA = """
        CREATE TABLE users (id INT PRIMARY KEY AUTO_INCREMENT, username VARCHAR(25) NOT NULL, password VARCHAR(64) NOT NULL DEFAULT '',
            mail VARCHAR(100) NOT NULL DEFAULT '', motto VARCHAR(127) NOT NULL DEFAULT '', look VARCHAR(256) NOT NULL DEFAULT '',
            gender ENUM('M','F') NOT NULL DEFAULT 'M', `rank` INT NOT NULL DEFAULT 1, credits INT NOT NULL DEFAULT 0,
            account_created INT NOT NULL DEFAULT 0, last_online INT NOT NULL DEFAULT 0, online ENUM('0','1','2') NOT NULL DEFAULT '0',
            auth_ticket VARCHAR(256) NOT NULL DEFAULT '', ip_register VARCHAR(45) NOT NULL DEFAULT '', ip_current VARCHAR(45) NOT NULL DEFAULT '');
        CREATE TABLE users_settings (user_id INT NOT NULL, respects_received INT NOT NULL DEFAULT 0, daily_respect_points INT NOT NULL DEFAULT 3,
            daily_pet_respect_points INT NOT NULL DEFAULT 3, guild_id INT NOT NULL DEFAULT 0, home_room INT NOT NULL DEFAULT 0,
            volume_system INT NOT NULL DEFAULT 100, volume_furni INT NOT NULL DEFAULT 100, volume_trax INT NOT NULL DEFAULT 100,
            block_following ENUM('0','1') NOT NULL DEFAULT '0', block_friendrequests ENUM('0','1') NOT NULL DEFAULT '0',
            block_roominvites ENUM('0','1') NOT NULL DEFAULT '0', block_camera_follow ENUM('0','1') NOT NULL DEFAULT '0',
            old_chat ENUM('0','1') NOT NULL DEFAULT '0', chat_color INT NOT NULL DEFAULT 0, ui_flags INT(11) NOT NULL DEFAULT 1,
            club_expire_timestamp INT NOT NULL DEFAULT 0, online_time INT NOT NULL DEFAULT 0,
            has_gotten_default_saved_searches TINYINT(1) NOT NULL DEFAULT 0);
        CREATE TABLE users_currency (user_id INT NOT NULL, type INT NOT NULL, amount INT NOT NULL, UNIQUE (user_id, type));
        CREATE TABLE users_subscriptions (id INT UNSIGNED PRIMARY KEY AUTO_INCREMENT, user_id INT UNSIGNED NULL,
            subscription_type VARCHAR(255) NULL, timestamp_start INT UNSIGNED NULL, duration INT UNSIGNED NULL, active TINYINT(1) NULL DEFAULT 1);
        CREATE TABLE permissions (id INT PRIMARY KEY, rank_name VARCHAR(25) NOT NULL, level INT(2) NOT NULL DEFAULT 1,
            cmd_ban ENUM('0','1') NOT NULL DEFAULT '0');
        CREATE TABLE room_models (name VARCHAR(100) NOT NULL, door_x INT NOT NULL, door_y INT NOT NULL, door_dir INT NOT NULL,
            heightmap TEXT NOT NULL, public_items TEXT NOT NULL, club_only ENUM('0','1') NOT NULL DEFAULT '0');
        CREATE TABLE rooms (id INT PRIMARY KEY AUTO_INCREMENT, owner_id INT NOT NULL, owner_name VARCHAR(25) NOT NULL DEFAULT '',
            name VARCHAR(50) NOT NULL DEFAULT '', description VARCHAR(512) NOT NULL DEFAULT '', model VARCHAR(100) NOT NULL DEFAULT 'model_a',
            password VARCHAR(20) NOT NULL DEFAULT '', state ENUM('open','locked','password','invisible') NOT NULL DEFAULT 'open',
            users INT NOT NULL DEFAULT 0, users_max INT NOT NULL DEFAULT 25, category INT NOT NULL DEFAULT 0, score INT NOT NULL DEFAULT 0,
            paper_floor VARCHAR(5) NOT NULL DEFAULT '0.0', paper_wall VARCHAR(5) NOT NULL DEFAULT '0.0', paper_landscape VARCHAR(5) NOT NULL DEFAULT '0.0',
            thickness_wall INT NOT NULL DEFAULT 0, wall_height INT NOT NULL DEFAULT -1, thickness_floor INT NOT NULL DEFAULT 0,
            moodlight_data VARCHAR(254) NOT NULL DEFAULT '', tags VARCHAR(500) NOT NULL DEFAULT '', is_public ENUM('0','1') NOT NULL DEFAULT '0',
            is_staff_picked ENUM('0','1') NOT NULL DEFAULT '0', allow_other_pets ENUM('0','1') NOT NULL DEFAULT '0',
            allow_other_pets_eat ENUM('0','1') NOT NULL DEFAULT '0', allow_walkthrough ENUM('0','1') NOT NULL DEFAULT '1',
            allow_hidewall ENUM('0','1') NOT NULL DEFAULT '0', chat_protection INT NOT NULL DEFAULT 2, who_can_mute INT NOT NULL DEFAULT 0,
            who_can_kick INT NOT NULL DEFAULT 0, who_can_ban INT NOT NULL DEFAULT 0, guild_id INT NOT NULL DEFAULT 0,
            override_model ENUM('0','1') NOT NULL DEFAULT '0', trade_mode INT NOT NULL DEFAULT 2);
        CREATE TABLE items_base (id INT PRIMARY KEY AUTO_INCREMENT, sprite_id INT NOT NULL DEFAULT 0, item_name VARCHAR(70) NOT NULL DEFAULT '',
            public_name VARCHAR(56) NOT NULL DEFAULT '', width INT NOT NULL DEFAULT 1, length INT NOT NULL DEFAULT 1,
            stack_height DOUBLE(4,2) NOT NULL DEFAULT 0.00, allow_stack ENUM('0','1') NOT NULL DEFAULT '1', allow_sit ENUM('0','1') NOT NULL DEFAULT '0',
            allow_lay ENUM('0','1') NOT NULL DEFAULT '0', allow_walk ENUM('0','1') NOT NULL DEFAULT '0', allow_gift ENUM('0','1') NOT NULL DEFAULT '1',
            allow_trade ENUM('0','1') NOT NULL DEFAULT '1', allow_recycle ENUM('0','1') NOT NULL DEFAULT '0',
            allow_marketplace_sell ENUM('0','1') NOT NULL DEFAULT '0', allow_inventory_stack ENUM('0','1') NOT NULL DEFAULT '1',
            type ENUM('s','i','e','h','v','r','b','p') NOT NULL DEFAULT 's', interaction_type VARCHAR(500) NOT NULL DEFAULT 'default',
            interaction_modes_count INT NOT NULL DEFAULT 1, vending_ids VARCHAR(255) NOT NULL DEFAULT '0',
            multiheight VARCHAR(50) NOT NULL DEFAULT '0', customparams VARCHAR(256) NOT NULL DEFAULT '',
            effect_id_male INT NOT NULL DEFAULT 0, effect_id_female INT NOT NULL DEFAULT 0, clothing_on_walk VARCHAR(255) NOT NULL DEFAULT '');
        CREATE TABLE items (id INT PRIMARY KEY AUTO_INCREMENT, user_id INT NOT NULL DEFAULT 0, room_id INT NOT NULL DEFAULT 0,
            item_id INT NOT NULL DEFAULT 0, wall_pos VARCHAR(20) NOT NULL DEFAULT '', x INT NOT NULL DEFAULT 0, y INT NOT NULL DEFAULT 0,
            z DOUBLE(10,6) NOT NULL DEFAULT 0.000000, rot INT NOT NULL DEFAULT 0, extra_data VARCHAR(1024) NOT NULL DEFAULT '',
            wired_data VARCHAR(10000) NOT NULL DEFAULT '', limited_data VARCHAR(10) NOT NULL DEFAULT '0:0', guild_id INT NOT NULL DEFAULT 0);
        CREATE TABLE items_teleports (teleport_one_id INT NOT NULL, teleport_two_id INT NOT NULL);
        CREATE TABLE users_pets (id INT PRIMARY KEY AUTO_INCREMENT, user_id INT NOT NULL, room_id INT NOT NULL DEFAULT 0, name VARCHAR(15) NOT NULL,
            race INT NOT NULL, type INT NOT NULL, color VARCHAR(6) NOT NULL, happyness INT NOT NULL DEFAULT 100, experience INT NOT NULL DEFAULT 0,
            energy INT NOT NULL DEFAULT 100, hunger INT NOT NULL DEFAULT 0, thirst INT NOT NULL DEFAULT 0, respect INT NOT NULL DEFAULT 0,
            created INT NOT NULL DEFAULT 0, x INT NOT NULL DEFAULT 0, y INT NOT NULL DEFAULT 0, z DOUBLE NOT NULL DEFAULT 0, rot INT NOT NULL DEFAULT 0,
            hair_style INT NOT NULL DEFAULT -1, hair_color INT NOT NULL DEFAULT 0, saddle ENUM('0','1') NOT NULL DEFAULT '0',
            ride ENUM('0','1') NOT NULL DEFAULT '0', mp_is_dead TINYINT(1) NOT NULL DEFAULT 0);
        CREATE TABLE bots (id INT PRIMARY KEY AUTO_INCREMENT, user_id INT NOT NULL, room_id INT NOT NULL DEFAULT 0, name VARCHAR(25) NOT NULL,
            motto VARCHAR(100) NOT NULL DEFAULT '', figure VARCHAR(500) NOT NULL, gender ENUM('M','F') NOT NULL DEFAULT 'M',
            x INT NOT NULL DEFAULT 0, y INT NOT NULL DEFAULT 0, z DOUBLE NOT NULL DEFAULT 0, rot INT NOT NULL DEFAULT 0,
            chat_lines VARCHAR(5112) NOT NULL DEFAULT '', chat_auto ENUM('0','1') NOT NULL DEFAULT '1', chat_random ENUM('0','1') NOT NULL DEFAULT '1',
            chat_delay INT NOT NULL DEFAULT 5, dance INT NOT NULL DEFAULT 0, freeroam ENUM('0','1') NOT NULL DEFAULT '0',
            type VARCHAR(500) NOT NULL DEFAULT 'generic', effect INT(3) NOT NULL DEFAULT 0, bubble_id INT(3) NULL DEFAULT 31);
        """;

    private async Task SeedSourceAsync()
    {
        await ExecuteAsync(null, $"CREATE DATABASE `{_source}` CHARACTER SET utf8mb4;");
        await ExecuteAsync(_source, SCHEMA);

        var now = Unix(DateTimeOffset.UtcNow);

        await ExecuteAsync(
            _source,
            $"""
            INSERT INTO permissions (id, rank_name, level) VALUES (1, 'User', 1), (7, 'Administrator', 7);
            INSERT INTO users (id, username, look, gender, `rank`, credits, account_created, online) VALUES
                (1, 'Owner', 'hr-100-61', 'F', 7, 1000, {now
                - 86400}, '1'), (2, 'Guest', 'hr-115-42', 'M', 1, 5, {now}, '0');
            INSERT INTO users_settings (user_id, respects_received, block_friendrequests, old_chat, has_gotten_default_saved_searches)
                VALUES (1, 9, '1', '1', 1);
            INSERT INTO users_currency VALUES (1, 0, 250), (1, 5, 12);
            INSERT INTO users_subscriptions (user_id, subscription_type, timestamp_start, duration, active)
                VALUES (1, 'HABBO_CLUB', {now - 86400}, {31 * 86400}, 1);
            INSERT INTO room_models (name, door_x, door_y, door_dir, heightmap, public_items) VALUES
                ('model_ms', 1, 1, 2, 'xxxxxxxxxx\r\nx000000000\r\nx000000000', '');
            INSERT INTO rooms (id, owner_id, owner_name, name, model, state, allow_walkthrough, tags) VALUES
                (1, 1, 'Owner', 'Big room', 'model_ms', 'locked', '0', 'a;b;');
            INSERT INTO items_base (id, sprite_id, item_name, type, interaction_type, interaction_modes_count) VALUES
                (1, 90001, 'ms_custom_chair', 's', 'default', 2), (2, 90002, 'ms_custom_teleport', 's', 'teleport', 3),
                (3, 90003, 'ms_custom_poster', 'i', 'default', 1), (4, 90004, 'wf_trg_walks_on_furni', 's', 'wf_trg_walks_on_furni', 2);
            INSERT INTO items (id, user_id, room_id, item_id, x, y, z, rot, extra_data) VALUES
                (1, 1, 1, 2, 1, 1, 0, 2, '0'), (2, 2, 0, 2, 0, 0, 0, 0, '0');
            INSERT INTO items_teleports VALUES (1, 2);
            INSERT INTO items (id, user_id, room_id, item_id, wall_pos, extra_data) VALUES (3, 1, 1, 3, ':w=4,1 l=12,33 l', '7');
            INSERT INTO items (id, user_id, room_id, item_id, x, y, wired_data) VALUES (4, 1, 1, 4, 2, 2, '{WALK_ON}');
            INSERT INTO users_pets (user_id, room_id, name, race, type, color, experience, created) VALUES (1, 1, 'Rex', 1, 0, 'FFFFFF', 150, {now});
            INSERT INTO bots (user_id, room_id, name, figure, chat_lines) VALUES (1, 1, 'Frank', 'hr-1', 'Hi\rThere\r');
            """
        );

        // A hotel's worth of chairs: one in a room, one in an inventory, alternately.
        await ExecuteAsync(
            _source,
            $"""
            INSERT INTO items (id, user_id, room_id, item_id, x, y, z, rot, extra_data, limited_data)
            SELECT 100 + i, 1 + (i % 2), IF(i % 2 = 0, 1, 0), 1, 1 + (i % 9), 1 + (i % 2), (i % 5) * 0.25, (i % 4) * 2,
                CAST(i % 2 AS CHAR), IF(i = 7, '50:3', '0:0')
            FROM (
                SELECT 1 + a.d + 10 * b.d + 100 * c.d + 1000 * e.d + 10000 * f.d AS i
                FROM {DIGITS} a CROSS JOIN {DIGITS} b CROSS JOIN {DIGITS} c CROSS JOIN {DIGITS} e CROSS JOIN {DIGITS} f
            ) n
            WHERE i <= {ITEMS};
            """
        );
    }

    [Fact]
    public async Task A_mysql_hotel_imports_into_a_migrated_turbo_database()
    {
        Assert.SkipUnless(SERVER is not null, "TURBO_TEST_MYSQL is not set");

        await SeedSourceAsync();

        await using (var db = Context())
        {
            var migrator = new DatabaseMigrator(
                Microsoft.Extensions.Options.Options.Create(
                    new DatabaseConfig { Migrate = MigrationMode.Auto }
                ),
                new MySqlMigrationLock(),
                new CapturingLogger<DatabaseMigrator>()
            );

            await migrator.MigrateAsync(db, "core", null, Ct);
        }

        var watch = Stopwatch.StartNew();

        await using (var db = Context())
        {
            var importer = new MorningstarImporter(
                db,
                Options(),
                NullLogger<MorningstarImporter>.Instance,
                TimeProvider.System
            );

            await importer.RunAsync(Connection(_source), Ct);
        }

        watch.Stop();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Imported {ITEMS:N0} items in {watch.Elapsed}"
        );

        await using var check = Context();

        var owner = await check.Players.SingleAsync(x => x.Id == 1, Ct);

        Assert.Equal(
            ("Owner", Turbo.Primitives.Rooms.Enums.AvatarGenderType.Female),
            (owner.Name, owner.Gender)
        );
        Assert.True(
            (
                await check.PlayerSettings.SingleAsync(x => x.PlayerEntityId == 1, Ct)
            ).FriendRequestsDisabled
        );
        Assert.Equal(1, await check.PlayerPermissionGroups.CountAsync(Ct));
        Assert.NotNull((await check.PlayerSubscriptions.SingleAsync(Ct)).ExpiresAt);

        var room = await check.Rooms.SingleAsync(Ct);

        Assert.Equal(("a,b", false), (room.Tags, room.AllowBlocking));
        Assert.Equal(
            "model_ms",
            (await check.RoomModels.SingleAsync(x => x.Id == room.RoomModelEntityId, Ct)).Name
        );

        Assert.Equal(ITEMS + 4, await check.Furnitures.CountAsync(Ct));
        Assert.Equal(
            ITEMS / 2 + 3,
            await check.Furnitures.CountAsync(x => x.RoomEntityId == 1, Ct)
        );

        var teleport = await check.Furnitures.SingleAsync(x => x.Id == 1, Ct);

        Assert.Equal(
            2,
            JsonNode.Parse(teleport.ExtraData!)!["room_linker"]!["ItemId"]!.GetValue<int>()
        );

        var poster = await check.Furnitures.SingleAsync(x => x.Id == 3, Ct);

        Assert.Equal((4, 1, 12, 33.0), (poster.X, poster.Y, poster.WallOffset, poster.Z));

        var lamp = JsonNode.Parse(
            (await check.Furnitures.SingleAsync(x => x.Id == 107, Ct)).ExtraData!
        )!["stuff"]!;

        Assert.Equal(3, lamp["UniqueNumber"]!.GetValue<int>());

        var trigger = JsonNode.Parse(
            (await check.Furnitures.SingleAsync(x => x.Id == 4, Ct)).ExtraData!
        )!;

        Assert.Equal(1, trigger["wired"]!["StuffIds"]![0]!.GetValue<int>());
        Assert.Equal(1, await check.Pets.CountAsync(Ct));
        Assert.Equal("Hi\nThere", (await check.Bots.SingleAsync(Ct)).ChatText);

        Assert.True(watch.Elapsed < TimeSpan.FromMinutes(2), $"import took {watch.Elapsed}");

        // A player made after the import takes the next id, not one of the imported.
        var newcomer = new PlayerEntity
        {
            Name = "Newcomer",
            Figure = "hr-1",
            Gender = Turbo.Primitives.Rooms.Enums.AvatarGenderType.Male,
            PlayerStatus = Turbo.Primitives.Players.Enums.PlayerStatusType.Offline,
        };

        check.Players.Add(newcomer);
        await check.SaveChangesAsync(Ct);

        Assert.Equal(3, newcomer.Id);
    }
}
