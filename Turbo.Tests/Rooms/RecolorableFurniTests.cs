using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Turbo.Database.Migrations;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Furniture.Smart;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The recolorable Builders Club blocks (WIN63-202610121624): the recolor window's
/// <c>RecolorFurniture</c> (furni id, scope 0-3, r, g, b) gives the furni, and by the scope the
/// room's other recolorable furni, a colour of their palette, and the room is told with one
/// <c>FurniRecolorUpdate</c> (int count; per entry int n, n furni ids, short r, g, b). A Builders
/// Club placement carries the catalogue's colour as its extra parameter <c>"r,g,b"</c>, and the
/// placed block's map data holds it as <c>r</c>, <c>g</c>, <c>b</c> and <c>state</c>, as in the
/// official client capture (BuildersClubPlaceRoomItem extra "255,54,0" -> ObjectAdd map
/// <c>r=255, g=54, b=0, state=0</c>).
/// </summary>
public sealed class RecolorableFurniTests
{
    private const int OWNER = 1;
    private const int VISITOR = 2;
    private const int BLOCK_DEFINITION = 19348;
    private const int LETTER_DEFINITION = 19320;
    private const string BLOCK = "bc_block_0_recolorable";
    private const string LETTER = "bc_alpha1_a_recolorable";
    private const int ORANGE = 0xFF3600;
    private const int BLUE = 0x123456;
    private const int TEAL = 0x00FFCC;

    private static readonly RoomConfig Config = new()
    {
        RecolorablePaletteToFurni = $"letters={LETTER}",
        RecolorablePalettes = new()
        {
            ["default"] = "FF3600,123456,ffffff",
            ["letters"] = "FF3600,00FFCC",
        },
    };

    private readonly WiredRoom _room = new(8, 8, Config);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_owner_recolors_one_block_and_the_room_is_told_once()
    {
        var block = Block(10, BLOCK, BLOCK_DEFINITION);
        var other = Block(11, BLOCK, BLOCK_DEFINITION);

        (await RecolorAsync(OWNER, 10, RecolorScopeType.OnlyThis, ORANGE)).Should().BeTrue();

        ColorOf(block).Should().Be(ORANGE);
        ColorOf(other).Should().Be(RecolorableData.WHITE);
        Broadcast<FurniRecolorUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Updates.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ObjectIds = new[] { (RoomObjectId)10 },
                    Red = 255,
                    Green = 54,
                    Blue = 0,
                }
            );
        Broadcast<ObjectDataUpdateMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_type_scope_takes_every_block_of_that_definition()
    {
        Block(10, BLOCK, BLOCK_DEFINITION);
        Block(11, BLOCK, BLOCK_DEFINITION);
        var letter = Block(12, LETTER, LETTER_DEFINITION);

        (await RecolorAsync(OWNER, 10, RecolorScopeType.SameType, BLUE)).Should().BeTrue();

        Broadcast<FurniRecolorUpdateMessageComposer>()
            .Single()
            .Updates.Single()
            .ObjectIds.Should()
            .BeEquivalentTo(new RoomObjectId[] { 10, 11 });
        ColorOf(letter).Should().Be(RecolorableData.WHITE);
    }

    [Fact]
    public async Task The_same_colour_scope_skips_a_block_whose_palette_lacks_the_colour()
    {
        Block(10, BLOCK, BLOCK_DEFINITION);
        Block(11, BLOCK, BLOCK_DEFINITION);
        var letter = Block(12, LETTER, LETTER_DEFINITION);
        await RecolorAsync(OWNER, 10, RecolorScopeType.OnlyThis, ORANGE);
        await RecolorAsync(OWNER, 12, RecolorScopeType.OnlyThis, ORANGE);

        // The letters' palette has no BLUE: the letter keeps its orange.
        (await RecolorAsync(OWNER, 10, RecolorScopeType.SameColor, BLUE))
            .Should()
            .BeTrue();

        Broadcast<FurniRecolorUpdateMessageComposer>()
            .Last()
            .Updates.Single()
            .ObjectIds.Should()
            .Equal((RoomObjectId)10);
        ColorOf(letter).Should().Be(ORANGE);
    }

    [Fact]
    public async Task Same_type_and_colour_takes_only_blocks_of_both()
    {
        Block(10, BLOCK, BLOCK_DEFINITION);
        Block(11, BLOCK, BLOCK_DEFINITION);
        var orangeElsewhere = Block(12, BLOCK, BLOCK_DEFINITION);
        await RecolorAsync(OWNER, 12, RecolorScopeType.OnlyThis, ORANGE);

        (await RecolorAsync(OWNER, 10, RecolorScopeType.SameTypeAndColor, BLUE)).Should().BeTrue();

        Broadcast<FurniRecolorUpdateMessageComposer>()
            .Last()
            .Updates.Single()
            .ObjectIds.Should()
            .BeEquivalentTo(new RoomObjectId[] { 10, 11 });
        ColorOf(orangeElsewhere).Should().Be(ORANGE);
    }

    [Fact]
    public async Task A_visitor_without_rights_or_a_colour_off_the_palette_changes_nothing()
    {
        var block = Block(10, BLOCK, BLOCK_DEFINITION);

        (await RecolorAsync(VISITOR, 10, RecolorScopeType.OnlyThis, ORANGE)).Should().BeFalse();
        (await RecolorAsync(OWNER, 10, RecolorScopeType.OnlyThis, TEAL)).Should().BeFalse();

        ColorOf(block).Should().Be(RecolorableData.WHITE);
        Broadcast<FurniRecolorUpdateMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task A_builders_club_placement_keeps_the_catalogue_colour()
    {
        var definition = Definition(BLOCK, BLOCK_DEFINITION);
        StubBorrow(definition);

        (
            await _room
                .Harness.Module<RoomFurniModule>()
                .PlaceBuildersClubFloorItemAsync(
                    ActionContext.CreateForPlayer(OWNER, 1),
                    1,
                    OFFER,
                    "255,54,0",
                    2,
                    2,
                    Rotation.North,
                    false,
                    Ct
                )
        )
            .Should()
            .BeTrue();

        var placed = _room
            .Harness.Module<RoomFurniModule>()
            .Items.OfType<IRoomFloorItem>()
            .Single(x => x.Definition.Id == BLOCK_DEFINITION);
        // In the official ObjectAdd's order: r, b, g, state.
        ((IMapStuffData)placed.Logic.StuffData)
            .Data.Should()
            .Equal(
                new KeyValuePair<string, string>("r", "255"),
                new KeyValuePair<string, string>("b", "0"),
                new KeyValuePair<string, string>("g", "54"),
                new KeyValuePair<string, string>("state", "0")
            );
    }

    [Theory]
    [InlineData("")]
    [InlineData("0,255,204")] // TEAL: not in the blocks' palette
    [InlineData("256,0,0")]
    public void A_parameter_off_the_palette_leaves_a_new_block_white(string extraParam)
    {
        var block = Block(10, BLOCK, BLOCK_DEFINITION);

        block.Logic.ApplyCatalogParam(extraParam);

        ColorOf(block).Should().Be(RecolorableData.WHITE);
        ((IMapStuffData)block.Logic.StuffData).Data.Keys.Should().NotContain(["r", "g", "b"]);
    }

    [Fact]
    public async Task The_client_bytes_recolor_through_the_handler()
    {
        var harness = new PacketHarness();
        var sent = new List<(long Id, object Interaction)>();
        harness.Fakes.Handlers["InteractWithItemAsync"] = call =>
        {
            sent.Add(((RoomObjectId)call.Args[1]!, call.Args[2]!));

            return Task.FromResult(true);
        };

        await harness.SendAsync(
            PacketHarness.Incoming("RecolorFurnitureMessageEvent"),
            PacketHarness.Payload(w => w.Int(10).Int(2).Int(255).Int(54).Int(0)),
            playerId: OWNER,
            roomId: 1
        );

        sent.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                (
                    10L,
                    (object)
                        new RecolorFurnitureInteraction
                        {
                            Scope = RecolorScopeType.SameType,
                            Red = 255,
                            Green = 54,
                            Blue = 0,
                        }
                )
            );
    }

    [Fact]
    public void FurniRecolorUpdate_writes_each_entry_as_ids_then_three_shorts()
    {
        var packet = PacketHarness.Encode(
            new FurniRecolorUpdateMessageComposer
            {
                Updates =
                [
                    new RecolorUpdateSnapshot
                    {
                        ObjectIds = [10, 11],
                        Red = 255,
                        Green = 54,
                        Blue = 0,
                    },
                ],
            }
        );

        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(10);
        packet.PopInt().Should().Be(11);
        packet.PopShort().Should().Be(255);
        packet.PopShort().Should().Be(54);
        packet.PopShort().Should().Be(0);
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void The_migration_gives_every_recolorable_class_the_logic_and_leaves_staff_choices()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Exec(
            connection,
            "CREATE TABLE furniture_definitions (name TEXT, logic TEXT);"
                + "INSERT INTO furniture_definitions VALUES "
                + "('bc_block_0_recolorable', 'default_floor'), ('bc_alpha1_z_recolorable', 'default_floor'), "
                + "('bc_block_0*1', 'default_floor'), ('xrecolorable', 'default_floor'), "
                + "('bc_stick_recolorable', 'dice');"
        );

        foreach (var sql in MigrationSql(new MapRecolorableFurnitureLogic(), up: true))
            Exec(connection, sql);

        Logics(connection)
            .Should()
            .Equal(
                ("bc_alpha1_z_recolorable", "recolorable"),
                ("bc_block_0*1", "default_floor"),
                ("bc_block_0_recolorable", "recolorable"),
                ("bc_stick_recolorable", "dice"),
                ("xrecolorable", "default_floor")
            );
        typeof(FurnitureRecolorableLogic)
            .GetCustomAttribute<Turbo.Primitives.Rooms.Object.Logic.RoomObjectLogicAttribute>()!
            .Key.Should()
            .Be("recolorable");

        foreach (var sql in MigrationSql(new MapRecolorableFurnitureLogic(), up: false))
            Exec(connection, sql);

        Logics(connection).Count(x => x.Logic == "recolorable").Should().Be(0);
    }

    [Fact]
    public void The_default_palettes_are_habbos()
    {
        var config = new RoomConfig();

        RecolorableData
            .GetPalette(config.RecolorablePaletteToFurni, config.RecolorablePalettes, BLOCK)
            .Should()
            .HaveCount(69)
            .And.StartWith([0xFFFFFF, 0xAEF0D8])
            .And.Contain(ORANGE);
        RecolorableData
            .GetPalette(
                config.RecolorablePaletteToFurni,
                config.RecolorablePalettes,
                "bc_block_grass_recolorable"
            )
            .Should()
            .Equal(0xB0B1AE, 0x6AB18E, 0x57832E, 0x30B106, 0x929E12, 0xB19626, 0xB15501);
    }

    [Fact]
    public async Task An_imported_block_on_the_default_logic_is_recolorable_and_steps_down_by_state()
    {
        var item = _room.AddFloorItem(20, 2, 2, BLOCK, BLOCK_DEFINITION);
        item.UseDefinition(
            item.Definition with
            {
                LogicName = "default_floor",
                TotalStates = 5,
                StackHeight = Altitude.FromInt(100),
                CustomParams = "-0.25",
            }
        );
        item.SetLogic(
            _room.Harness.LogicProvider.CreateLogicInstance(
                "default_floor",
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        await item.Logic.SetStateAsync(2);

        item.Logic.Should().BeOfType<FurnitureRecolorableLogic>();
        item.GetStackHeight().ToInt().Should().Be(50);
        Broadcast<ObjectUpdateMessageComposer>()
            .Should()
            .Contain(x => x.FloorItem.ObjectId == 20 && x.FloorItem.StackHeight.ToInt() == 50);
    }

    [Fact]
    public async Task The_smart_variables_read_the_colour_and_write_only_palette_colours()
    {
        var block = Block(10, BLOCK, BLOCK_DEFINITION);
        await RecolorAsync(OWNER, 10, RecolorScopeType.OnlyThis, ORANGE);
        var room = _room.Harness.Room;
        var ctx = _room.Harness.Fakes.Create<IWiredExecutionContext>();
        var rgb = new FurnitureRecolorableRgbVariable(room);
        var red = new FurnitureRecolorableRedVariable(room);
        var green = new FurnitureRecolorableGreenVariable(room);
        var blue = new FurnitureRecolorableBlueVariable(room);

        ValueOf(rgb, 10).Should().Be(ORANGE);
        (ValueOf(red, 10), ValueOf(green, 10), ValueOf(blue, 10)).Should().Be((255, 54, 0));
        // As the official server lists them: rgb writable with the text connector flag, the channels read only.
        rgb.GetVarSnapshot()
            .Flags.Should()
            .Be(
                WiredVariableFlags.HasValue
                    | WiredVariableFlags.CanWriteValue
                    | WiredVariableFlags.HasTextConnector
            );
        red.GetVarSnapshot().VariableName.Should().Be("~recolorable_furni.color.rgb.red");
        red.GetVarSnapshot().Flags.Should().Be(WiredVariableFlags.HasValue);

        (await rgb.SetValueAsync(ctx, Key(rgb, 10), BLUE)).Should().BeTrue();
        ColorOf(block).Should().Be(BLUE);
        Broadcast<FurniRecolorUpdateMessageComposer>()
            .Last()
            .Updates.Single()
            .Should()
            .BeEquivalentTo(
                new
                {
                    ObjectIds = new[] { (RoomObjectId)10 },
                    Red = 0x12,
                    Green = 0x34,
                    Blue = 0x56,
                }
            );

        // #FF3456 is in no palette; #FFFFFF is.
        (await red.SetValueAsync(ctx, Key(red, 10), 255))
            .Should()
            .BeFalse();
        ColorOf(block).Should().Be(BLUE);
        (await rgb.SetValueAsync(ctx, Key(rgb, 10), 0x1000000)).Should().BeFalse();
        (await rgb.SetValueAsync(ctx, Key(rgb, 10), 0xFFFFFF)).Should().BeTrue();
        (await blue.SetValueAsync(ctx, Key(blue, 10), 0)).Should().BeFalse();
        ColorOf(block).Should().Be(0xFFFFFF);
    }

    private static int ValueOf(WiredInternalVariable variable, int itemId)
    {
        variable.TryGetValue(Key(variable, itemId), out var value).Should().BeTrue();

        return (int)value;
    }

    private static WiredVariableKey Key(WiredInternalVariable variable, int itemId) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.Furni, itemId);

    private const int OFFER = 35202;

    private IRoomFloorItem Block(int id, string name, int definitionId)
    {
        var item = _room.AddFloorItem(
            id,
            id % 8,
            1,
            "recolorable",
            definitionId,
            createLogic: (factory, ctx) => new FurnitureRecolorableLogic(factory, ctx)
        );
        item.UseDefinition(item.Definition with { Name = name });

        return item;
    }

    private static FurnitureDefinitionSnapshot Definition(string name, int id)
    {
        var item = new WiredRoom().AddFloorItem(1, 0, 0, "recolorable", id);

        return item.Definition with
        {
            Name = name,
        };
    }

    private void StubBorrow(FurnitureDefinitionSnapshot definition)
    {
        var fakes = _room.Harness.Fakes;
        var product = (CatalogProductSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(CatalogProductSnapshot)) with
        {
            ProductType = ProductType.Floor,
            FurniDefinitionId = definition.Id,
        };
        var offer = (CatalogOfferSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(CatalogOfferSnapshot)) with
        {
            Visible = true,
            Products = ImmutableArray.Create(product),
        };
        var catalog = (CatalogSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(CatalogSnapshot)) with
        {
            OffersById = ImmutableDictionary<int, CatalogOfferSnapshot>.Empty.Add(OFFER, offer),
        };
        var subscription = (PlayerSubscriptionSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(PlayerSubscriptionSnapshot)) with
        {
            SecondsLeft = 3600,
            FurniLimit = 100,
        };

        fakes.Handlers["GetCatalogSnapshot"] = _ => catalog;
        fakes.Handlers["TryGetDefinition"] = _ => definition;
        fakes.Handlers["GetAsync"] = call =>
            call.Interface == typeof(IPlayerSubscriptionGrain)
                ? Task.FromResult(subscription)
                : Fakes.NotHandled;
        fakes.Handlers["CreateFromDefinition"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                OwnerId = OWNER,
                OwnerName = string.Empty,
                Definition = definition,
            };
            item.SetExtraData(null);

            return item;
        };
        fakes.Handlers["InsertBuildersClubItemAsync"] = _ => Task.FromResult(true);
    }

    private Task<bool> RecolorAsync(int playerId, int objectId, RecolorScopeType scope, int rgb) =>
        _room.Harness.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            objectId,
            new RecolorFurnitureInteraction
            {
                Scope = scope,
                Red = RecolorableData.Red(rgb),
                Green = RecolorableData.Green(rgb),
                Blue = RecolorableData.Blue(rgb),
            },
            Ct
        );

    private static int ColorOf(IRoomFloorItem item) =>
        ((FurnitureRecolorableLogic)item.Logic).Color;

    private IEnumerable<T> Broadcast<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();

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
