using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Turbo.Database.Migrations;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A one-way gate, as the official client capture of 2026-10-17 has one at direction 2: entered
/// from the tile its direction points at, it sends OneWayDoorStatus 1, walks the avatar through
/// to the tile behind and sends 0 as it arrives; from behind nothing happens. The item's data
/// never changes, and the message carries a third int, 0.
/// </summary>
public sealed class OneWayDoorTests
{
    private const int GATE = 90;
    private const int OWNER_AVATAR = 1;
    private const int OWNER = 100 + OWNER_AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10, avatarContexts: true);
    private long _now;

    public OneWayDoorTests()
    {
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)OWNER,
        };
    }

    [Fact]
    public async Task Entered_from_the_front_it_walks_the_avatar_through_and_shuts_on_arrival()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 6, 5);
        var gate = AddGate(5, 5, Rotation.East);
        var legacy = gate.Logic.GetLegacyString();

        (await EnterAsync()).Should().BeTrue();
        Statuses().Should().Equal(1);

        var gateIdx = _room.Map.ToIdx(5, 5);
        var path = new List<(int, int)>();
        bool? occupiedAtShut = null;

        for (var tick = 0; tick < 30 && (avatar.IsWalking || occupiedAtShut is null); tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            path.Add((avatar.X, avatar.Y));
            await RunTimersAsync();

            if (occupiedAtShut is null && Statuses().Count() == 2)
                occupiedAtShut = _room.TileFlags()[gateIdx].Has(RoomTileFlags.AvatarOccupied);
        }

        path.Distinct().Should().Equal((6, 5), (5, 5), (4, 5));
        Statuses().Should().Equal(1, 0);
        occupiedAtShut
            .Should()
            .BeFalse("it shuts once the avatar is off it, on its way to the back");
        gate.Logic.GetLegacyString().Should().Be(legacy);
        _room.Harness.Fakes.SentToRoom<ObjectDataUpdateMessageComposer>().Should().BeEmpty();
        _room.TileFlags()[gateIdx].Has(RoomTileFlags.Walkable).Should().BeFalse();
    }

    [Fact]
    public async Task Entered_on_the_last_step_onto_the_front_tile_it_lets_the_avatar_through()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 7, 5);
        AddGate(5, 5, Rotation.East);

        (
            await _room.Harness.Room.WalkAvatarToAsync(
                ActionContext.CreateForPlayer(OWNER, 1),
                6,
                5,
                Ct
            )
        )
            .Should()
            .BeTrue();
        _now += 10_000;
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);

        (await EnterAsync()).Should().BeTrue();

        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }

        (avatar.X, avatar.Y).Should().Be((4, 5));
    }

    [Fact]
    public async Task Entered_from_behind_nothing_happens()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 4, 5);
        AddGate(5, 5, Rotation.East);

        (await EnterAsync()).Should().BeFalse();

        Statuses().Should().BeEmpty();
        avatar.IsWalking.Should().BeFalse();
        (avatar.X, avatar.Y).Should().Be((4, 5));
    }

    [Fact]
    public async Task A_shut_gate_cannot_be_walked_onto()
    {
        _room.Enter(OWNER_AVATAR, 6, 5);
        AddGate(5, 5, Rotation.East);

        (
            await _room.Harness.Room.WalkAvatarToAsync(
                ActionContext.CreateForPlayer(OWNER, 1),
                5,
                5,
                Ct
            )
        )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void The_status_carries_a_third_int_zero()
    {
        var packet = PacketHarness.Encode(
            new OneWayDoorStatusMessageComposer { FurniId = 7, Status = 1 }
        );

        (packet.PopInt(), packet.PopInt(), packet.PopInt()).Should().Be((7, 1, 0));
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void The_migration_gives_the_one_way_gates_their_logic_and_leaves_staff_choices()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Exec(
            connection,
            "CREATE TABLE furniture_definitions (name TEXT, logic TEXT);"
                + "INSERT INTO furniture_definitions VALUES ('one_way_door*1', 'gate'), "
                + "('one_way_door*9', 'default_floor'), ('onewaydoor_c22_rosegold', 'gate'), "
                + "('one_way_door*5', 'teleport'), ('gate_x', 'gate');"
        );

        foreach (var sql in MigrationSql(new MapOneWayDoorLogic(), up: true))
            Exec(connection, sql);

        Logics(connection)
            .Should()
            .Equal(
                ("gate_x", "gate"),
                ("one_way_door*1", "one_way_door"),
                ("one_way_door*5", "teleport"),
                ("one_way_door*9", "one_way_door"),
                ("onewaydoor_c22_rosegold", "one_way_door")
            );

        foreach (var sql in MigrationSql(new MapOneWayDoorLogic(), up: false))
            Exec(connection, sql);

        Logics(connection)
            .Should()
            .Equal(
                ("gate_x", "gate"),
                ("one_way_door*1", "gate"),
                ("one_way_door*5", "teleport"),
                ("one_way_door*9", "gate"),
                ("onewaydoor_c22_rosegold", "gate")
            );
    }

    private Task<bool> EnterAsync() =>
        _room.Harness.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            GATE,
            new EnterOneWayDoorInteraction(),
            Ct
        );

    private IEnumerable<int> Statuses() =>
        _room.Harness.Fakes.SentToRoom<OneWayDoorStatusMessageComposer>().Select(x => x.Status);

    // One avatar tick ahead: far enough for the gate's own timer, short of its fallback.
    private Task RunTimersAsync() =>
        _room.Harness.Module<RoomTimerSystem>().ProcessTimersAsync(_room.Harness.NowMs() + 600, Ct);

    private IRoomFloorItem AddGate(int x, int y, Rotation rotation)
    {
        var item = new RoomFloorItem
        {
            ObjectId = GATE,
            OwnerId = OWNER,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000 + GATE,
                SpriteId = 1000 + GATE,
                Name = "one_way_door*1",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "one_way_door",
                TotalStates = 2,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.Zero,
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Controller,
                CustomParams = "",
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(rotation);
        item.SetLogic(
            new FurnitureOneWayDoorLogic(
                _room.StuffData,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        _room.Harness.State.ItemsById.Add((RoomObjectId)GATE, item);
        _room.Map.AddItem(item);

        return item;
    }

    private static IEnumerable<string> MigrationSql(Migration migration, bool up)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        typeof(Migration)
            .GetMethod(
                up ? "Up" : "Down",
                BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(MigrationBuilder)]
            )!
            .Invoke(migration, [builder]);

        return builder.Operations.OfType<SqlOperation>().Select(x => x.Sql.Replace('`', '"'));
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static List<(string Name, string Logic)> Logics(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, logic FROM furniture_definitions ORDER BY name;";
        using var reader = command.ExecuteReader();
        var rows = new List<(string, string)>();

        while (reader.Read())
            rows.Add((reader.GetString(0), reader.GetString(1)));

        return rows;
    }
}
