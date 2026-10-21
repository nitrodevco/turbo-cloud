using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Turbo.Database.Migrations;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED labels (WIN63-202610121624, released 2026-10-13): a <c>wf_label</c> furni holds a text,
/// a bubble colour, an audience and "always open". Every entry sends the room's labels right
/// after <c>WiredEnvironment</c>, none as <c>00000000</c> (official client capture); the bubble
/// (<c>SetFurnitureLabelData</c>) and the settings window (<c>SetFurnitureLabelSettings</c>)
/// change them for whoever has the room's wired modify permission
/// (<c>FurnitureLabelWidgetHandler.canModify</c>), and the room is told with
/// <c>FurnitureLabelsAndRegionsUpdate</c>.
/// </summary>
public sealed class WiredLabelTests
{
    private const int OWNER = 1;
    private const int VISITOR = 2;
    private const int TEAL = 2651759;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public void Whoever_walks_in_is_told_the_labels_after_the_wired_environment_even_none()
    {
        var sent = Wired.GetEntryState().ToList();
        var labels = sent.OfType<FurnitureLabelsAndRegionsMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        labels.Entries.Should().BeEmpty();
        sent.FindIndex(x => x is FurnitureLabelsAndRegionsMessageComposer)
            .Should()
            .BeGreaterThan(sent.FindIndex(x => x is WiredEnvironmentMessageComposer));
        PacketHarness.Encode(labels).PopInt().Should().Be(0);
    }

    [Fact]
    public async Task A_room_with_labels_lists_each_with_its_settings()
    {
        Label(10);
        Label(11);
        await WriteAsync(OWNER, 11, "Start here");

        Wired
            .GetEntryState()
            .OfType<FurnitureLabelsAndRegionsMessageComposer>()
            .Single()
            .Entries.Should()
            .BeEquivalentTo([
                new FurnitureLabelSnapshot
                {
                    ObjectId = 10,
                    Text = "",
                    BubbleColor = FurnitureLabelData.DEFAULT_COLOR,
                    Visibility = FurnitureLabelVisibilityType.WiredInspectors,
                    AlwaysOpen = false,
                },
                new FurnitureLabelSnapshot
                {
                    ObjectId = 11,
                    Text = "Start here",
                    BubbleColor = FurnitureLabelData.DEFAULT_COLOR,
                    Visibility = FurnitureLabelVisibilityType.WiredInspectors,
                    AlwaysOpen = false,
                },
            ]);
    }

    [Fact]
    public async Task The_owner_writes_a_label_and_the_room_is_told_a_visitor_changes_nothing()
    {
        var label = Label(10);

        (await WriteAsync(VISITOR, 10, "nope")).Should().BeFalse();
        _room
            .Harness.Fakes.SentToRoom<FurnitureLabelsAndRegionsUpdateMessageComposer>()
            .Should()
            .BeEmpty();

        (await WriteAsync(OWNER, 10, "Hello")).Should().BeTrue();

        LabelOf(label).Text.Should().Be("Hello");
        var update = _room
            .Harness.Fakes.SentToRoom<FurnitureLabelsAndRegionsUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        update.AddedOrUpdated.Should().ContainSingle().Which.Text.Should().Be("Hello");
        update.RemovedIds.Should().BeEmpty();
        // The text is kept with the item, so it is there after the room loads again.
        label.ExtraData.GetJsonString().Should().Contain("Hello");
    }

    [Fact]
    public async Task A_text_save_is_answered_with_the_furni_data_first_and_unchanged_settings_get_nothing()
    {
        Label(10);

        await WriteAsync(OWNER, 10, "parity label");

        // Official client capture 2026-10-13 11:18: ObjectDataUpdate (data ""), then the label's update.
        var sent = _room.Harness.Fakes.SentToRoom<IComposer>().ToList();
        sent.FindIndex(x => x is ObjectDataUpdateMessageComposer { ObjectId.Value: 10 })
            .Should()
            .BeGreaterThanOrEqualTo(0)
            .And.BeLessThan(
                sent.FindIndex(x => x is FurnitureLabelsAndRegionsUpdateMessageComposer)
            );

        (
            await SettingsAsync(
                OWNER,
                10,
                FurnitureLabelData.DEFAULT_COLOR,
                FurnitureLabelVisibilityType.WiredInspectors,
                false
            )
        )
            .Should()
            .BeTrue();

        _room.Harness.Fakes.SentToRoom<IComposer>().Should().HaveCount(sent.Count);
    }

    [Fact]
    public async Task Settings_keep_always_open_only_for_a_label_everyone_sees()
    {
        var label = Label(10);

        (await SettingsAsync(OWNER, 10, TEAL, FurnitureLabelVisibilityType.Everyone, true))
            .Should()
            .BeTrue();
        LabelOf(label)
            .Should()
            .BeEquivalentTo(
                new
                {
                    BubbleColor = TEAL,
                    Visibility = FurnitureLabelVisibilityType.Everyone,
                    AlwaysOpen = true,
                }
            );

        (await SettingsAsync(OWNER, 10, TEAL, FurnitureLabelVisibilityType.WiredInspectors, true))
            .Should()
            .BeTrue();
        LabelOf(label).AlwaysOpen.Should().BeFalse();
        _room
            .Harness.Fakes.SentToRoom<FurnitureLabelsAndRegionsUpdateMessageComposer>()
            .Should()
            .HaveCount(2);

        (
            await SettingsAsync(
                VISITOR,
                10,
                FurnitureLabelData.DEFAULT_COLOR,
                FurnitureLabelVisibilityType.Everyone,
                false
            )
        )
            .Should()
            .BeFalse();
        LabelOf(label).BubbleColor.Should().Be(TEAL);
    }

    [Fact]
    public async Task A_text_longer_than_the_bubble_takes_is_cut_at_its_limit()
    {
        var label = Label(10);

        await WriteAsync(OWNER, 10, new string('a', 1200));

        LabelOf(label).Text.Should().HaveLength(1000);
    }

    [Fact]
    public async Task A_placed_label_is_announced_and_a_picked_up_one_removed()
    {
        var item = new RoomFloorItem
        {
            ObjectId = 20,
            OwnerId = OWNER,
            OwnerName = "test",
            Definition = _room.AddFloorItem(1, 7, 7).Definition with
            {
                Id = 19398,
                SpriteId = 19398,
                Name = "wf_label",
                LogicName = "wf_label",
            },
        };
        item.SetExtraData(null);
        var ctx = ActionContext.CreateForPlayer(OWNER, 1);

        (
            await _room
                .Harness.Module<RoomFurniModule>()
                .PlaceFloorItemAsync(ctx, item, 3, 3, Rotation.North, Ct)
        )
            .Should()
            .BeTrue();

        item.Logic.Should().BeOfType<FurnitureLabelLogic>();
        _room
            .Harness.Fakes.SentToRoom<FurnitureLabelsAndRegionsUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.AddedOrUpdated.Should()
            .ContainSingle()
            .Which.ObjectId.Should()
            .Be((RoomObjectId)20);

        (await _room.Harness.Module<RoomObjectModule>().RemoveObjectAsync(ctx, item, Ct))
            .Should()
            .BeTrue();

        _room
            .Harness.Fakes.SentToRoom<FurnitureLabelsAndRegionsUpdateMessageComposer>()
            .Last()
            .RemovedIds.Should()
            .Equal((RoomObjectId)20);
    }

    [Fact]
    public async Task The_client_bytes_reach_the_label_through_the_handlers()
    {
        var harness = new PacketHarness();
        var sent = new List<object>();
        harness.Fakes.Handlers["InteractWithItemAsync"] = call =>
        {
            sent.Add(call.Args[2]!);

            return Task.FromResult(true);
        };

        await harness.SendAsync(
            PacketHarness.Incoming("SetFurnitureLabelDataMessageEvent"),
            PacketHarness.Payload(w => w.Int(10).String("Hi")),
            playerId: OWNER,
            roomId: 1
        );
        await harness.SendAsync(
            PacketHarness.Incoming("SetFurnitureLabelSettingsMessageEvent"),
            PacketHarness.Payload(w => w.Int(10).Int(TEAL).Int(0).Bool(true)),
            playerId: OWNER,
            roomId: 1
        );
        // A visibility the settings window never sends goes nowhere.
        await harness.SendAsync(
            PacketHarness.Incoming("SetFurnitureLabelSettingsMessageEvent"),
            PacketHarness.Payload(w => w.Int(10).Int(TEAL).Int(7).Bool(true)),
            playerId: OWNER,
            roomId: 1
        );

        sent.Should()
            .Equal(
                new SetFurnitureLabelTextInteraction { Text = "Hi" },
                new SetFurnitureLabelSettingsInteraction
                {
                    BubbleColor = TEAL,
                    Visibility = FurnitureLabelVisibilityType.Everyone,
                    AlwaysOpen = true,
                }
            );
    }

    [Fact]
    public void Entries_are_written_as_the_client_reads_them_labels_and_regions()
    {
        var packet = PacketHarness.Encode(
            new FurnitureLabelsAndRegionsUpdateMessageComposer
            {
                AddedOrUpdated =
                [
                    new FurnitureLabelSnapshot
                    {
                        ObjectId = 10,
                        Text = "Hi",
                        BubbleColor = TEAL,
                        Visibility = FurnitureLabelVisibilityType.Everyone,
                        AlwaysOpen = true,
                    },
                    new FurnitureLabelSnapshot
                    {
                        ObjectId = 11,
                        Text = "Lobby",
                        Region = new FurnitureRegionSnapshot
                        {
                            Description = "Wait here",
                            XOffset = 1,
                            YOffset = 2,
                            MinWidth = 3,
                            MinHeight = 4,
                            Color = "ff0000",
                            Areas =
                            [
                                new FurnitureRegionAreaSnapshot
                                {
                                    X = 5,
                                    Y = 6,
                                    Width = 7,
                                    Height = 8,
                                },
                            ],
                        },
                    },
                ],
                RemovedIds = [12],
            }
        );

        packet.PopInt().Should().Be(2);
        (packet.PopInt(), packet.PopInt(), packet.PopString()).Should().Be((0, 10, "Hi"));
        (packet.PopInt(), packet.PopInt(), packet.PopBoolean()).Should().Be((TEAL, 0, true));
        (packet.PopInt(), packet.PopInt(), packet.PopString(), packet.PopString())
            .Should()
            .Be((1, 11, "Lobby", "Wait here"));
        (packet.PopInt(), packet.PopInt(), packet.PopInt(), packet.PopInt())
            .Should()
            .Be((1, 2, 3, 4));
        packet.PopString().Should().Be("ff0000");
        packet.PopInt().Should().Be(1);
        (packet.PopInt(), packet.PopInt(), packet.PopInt(), packet.PopInt())
            .Should()
            .Be((5, 6, 7, 8));
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(12);
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void The_migration_gives_wf_label_its_logic_and_leaves_staff_choices()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Exec(
            connection,
            "CREATE TABLE furniture_definitions (name TEXT, logic TEXT);"
                + "INSERT INTO furniture_definitions VALUES ('wf_label', 'default_floor'), ('wf_label_x', 'default_floor');"
        );

        foreach (var sql in MigrationSql(new MapWiredLabelLogic(), up: true))
            Exec(connection, sql);

        Logics(connection)
            .Should()
            .Equal(("wf_label", "wf_label"), ("wf_label_x", "default_floor"));

        foreach (var sql in MigrationSql(new MapWiredLabelLogic(), up: false))
            Exec(connection, sql);

        Logics(connection)
            .Should()
            .Equal(("wf_label", "default_floor"), ("wf_label_x", "default_floor"));
    }

    private IRoomFloorItem Label(int id) =>
        _room.AddFloorItem(
            id,
            id % 8,
            2,
            "wf_label",
            createLogic: (factory, ctx) => new FurnitureLabelLogic(factory, ctx)
        );

    private static FurnitureLabelData LabelOf(IRoomFloorItem item) =>
        ((FurnitureLabelLogic)item.Logic).Label;

    private Task<bool> WriteAsync(int playerId, int objectId, string text) =>
        _room.Harness.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            objectId,
            new SetFurnitureLabelTextInteraction { Text = text },
            Ct
        );

    private Task<bool> SettingsAsync(
        int playerId,
        int objectId,
        int color,
        FurnitureLabelVisibilityType visibility,
        bool alwaysOpen
    ) =>
        _room.Harness.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            objectId,
            new SetFurnitureLabelSettingsInteraction
            {
                BubbleColor = color,
                Visibility = visibility,
                AlwaysOpen = alwaysOpen,
            },
            Ct
        );

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
