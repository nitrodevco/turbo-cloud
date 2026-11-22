using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Import.MorningstarImportPlayersTests;

namespace Turbo.Tests.Import;

/// <summary>
/// A Morningstar hotel's furniture imported into a Turbo database: where each item stands, and
/// the state its Turbo logic reads (Turbo refuses a room whose furni state does not parse).
/// </summary>
public class MorningstarImportFurnitureTests
{
    private const int THRONE = 500;

    private static MorningstarDb Hotel()
    {
        var ms = MorningstarImportPlayersTests.Hotel();

        ms.Insert(
                "room_models",
                new
                {
                    name = "model_a",
                    door_x = 1,
                    door_y = 1,
                    door_dir = 2,
                    heightmap = "xxxx\r\nx000\r\nx000",
                }
            )
            .Insert(
                "rooms",
                new
                {
                    id = 100,
                    owner_id = 10,
                    name = "Alice's place",
                    model = "model_a",
                    state = "open",
                    override_model = "0",
                    moodlight_data = "2,1,1,#FF00FF,200;1,2,2,#123456,80;1,3,1,#000000,255;",
                }
            );

        // Item bases: one Turbo has (by classname), the rest custom.
        Base(ms, 1, "throne", "s", "default", sprite: 999);
        Base(ms, 2, "teleport_door", "s", "teleport");
        Base(ms, 3, "poster", "i", "default");
        Base(ms, 4, "trophy_gold", "s", "trophy");
        Base(ms, 5, "dimmer_basic", "i", "dimmer");
        Base(ms, 6, "post_it", "i", "postit");
        Base(ms, 7, "present_gen", "s", "gift");
        Base(ms, 8, "wf_act_toggle_state", "s", "wf_act_toggle_state");
        Base(ms, 9, "wf_act_match_to_sshot", "s", "wf_act_match_to_sshot");
        Base(ms, 10, "jukebox1", "s", "jukebox");
        Base(ms, 11, "song_disk", "s", "musicdisc");
        Base(ms, 12, "rare_dragonlamp", "s", "default");
        Base(ms, 13, "fx_badge", "e", "default");

        Item(ms, 1000, 10, 100, 1, x: 1, y: 1, z: 0.5, rot: 2, extra: "0");
        Item(ms, 1001, 10, 100, 2, x: 2, y: 1);
        Item(ms, 1002, 11, 0, 2);
        ms.Insert("items_teleports", new { teleport_one_id = 1001, teleport_two_id = 1002 });
        Item(ms, 1003, 10, 100, 3, wallPos: ":w=3,2 l=10,40 r", extra: "12");
        Item(ms, 1004, 10, 0, 4, extra: "Alice\t5-3-2024\tWell done");
        Item(ms, 1005, 10, 100, 5, wallPos: ":w=1,1 l=5,30 l", extra: "2,1,2,#000000,255");
        Item(ms, 1006, 10, 100, 6, wallPos: ":w=2,1 l=1,2 r", extra: "9CCEFF Hello");
        Item(ms, 1007, 0, 0, 1, extra: "0");
        Item(ms, 1008, 11, 0, 7, extra: "1\t1007\t3\t5\t1\tEnjoy\tAlice\thr-1");
        Item(ms, 1009, 10, 100, 8, x: 2, y: 2, wired: """{"delay":4,"itemIds":[1000,1003]}""");
        Item(
            ms,
            1010,
            10,
            100,
            9,
            x: 3,
            y: 2,
            wired: """{"state":true,"direction":true,"position":false,"items":[{"item_id":1000,"state":"1","rotation":4,"x":1,"y":1}],"delay":0}"""
        );
        Item(ms, 1011, 10, 100, 10, x: 3, y: 1);
        Item(ms, 1012, 10, -1, 11, extra: "Alice\n1\n1\n2024\n120\nMy song\n7");
        ms.Insert("trax_playlist", new { trax_item_id = 1011, item_id = 1012 });
        ms.Insert(
            "soundtracks",
            new
            {
                id = 7,
                name = "My song",
                author = "Alice",
                code = "my_song",
                track = "1:1,4;",
                length = 120,
            }
        );
        Item(ms, 1013, 10, 0, 12, extra: "0", limited: "100:37");
        Item(ms, 1014, 11, 555, 1, x: 1, y: 1);
        Item(ms, 1015, 999, 100, 1, x: 1, y: 2);
        Item(ms, 1016, -1, 0, 1);
        ms.Insert(
            "marketplace_items",
            new
            {
                id = 1,
                item_id = 1016,
                user_id = 11,
                price = 10,
                state = 1,
            }
        );
        Item(ms, 1017, 10, 0, 77);

        return ms;
    }

    private static void Base(
        MorningstarDb ms,
        int id,
        string name,
        string type,
        string interaction,
        int? sprite = null
    ) =>
        ms.Insert(
            "items_base",
            new
            {
                id,
                sprite_id = sprite ?? 3000 + id,
                item_name = name,
                public_name = name,
                width = 1,
                length = 1,
                stack_height = 1.0,
                allow_stack = "1",
                allow_walk = "0",
                allow_sit = "0",
                allow_lay = "0",
                allow_trade = "1",
                type,
                interaction_type = interaction,
                interaction_modes_count = 2,
            }
        );

    private static void Item(
        MorningstarDb ms,
        int id,
        int owner,
        int room,
        int baseId,
        int x = 0,
        int y = 0,
        double z = 0,
        int rot = 0,
        string wallPos = "",
        string extra = "",
        string wired = "",
        string limited = "0:0"
    ) =>
        ms.Insert(
            "items",
            new
            {
                id,
                user_id = owner,
                room_id = room,
                item_id = baseId,
                wall_pos = wallPos,
                x,
                y,
                z,
                rot,
                extra_data = extra,
                wired_data = wired,
                limited_data = limited,
                guild_id = 0,
            }
        );

    private static SqliteDb Target()
    {
        var target = new SqliteDb();

        SeedGroups(target);
        target.Insert(
            new FurnitureDefinitionEntity
            {
                Id = THRONE,
                SpriteId = 230,
                Name = "throne",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                Logic = "default_floor",
                TotalStates = 1,
                Width = 1,
                Length = 1,
                StackHeight = 1,
                CanStack = true,
                CanWalk = false,
                CanSit = true,
                CanLay = false,
                CanRecycle = false,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
            }
        );

        return target;
    }

    private static JsonObject Extra(FurnitureEntity item) =>
        JsonNode.Parse(item.ExtraData!)!.AsObject();

    /// <summary>A section as the room reads it.</summary>
    private static TSection Section<TSection>(FurnitureEntity item, string section)
        where TSection : class =>
        FurnitureExtraDataSections.ReadDefinition<TSection>(
            item.ExtraData,
            section,
            NullLogger.Instance
        )!;

    [Fact]
    public async Task Definitions_are_matched_by_classname_or_created_with_their_logic()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        // Turbo's own throne, though Morningstar's sprite id differs.
        Assert.Equal(
            THRONE,
            (await db.Furnitures.SingleAsync(x => x.Id == 1000, ct)).FurnitureDefinitionEntityId
        );
        Assert.Equal(1, await db.FurnitureDefinitions.CountAsync(x => x.Name == "throne", ct));

        var logics = await db.FurnitureDefinitions.ToDictionaryAsync(
            x => x.Name,
            x => (x.Logic, x.ProductType, x.FurniCategory),
            ct
        );

        Assert.Equal(
            ("teleport", ProductType.Floor, FurnitureCategory.Default),
            logics["teleport_door"]
        );
        Assert.Equal(
            ("default_wall", ProductType.Wall, FurnitureCategory.Poster),
            logics["poster"]
        );
        Assert.Equal(
            ("dimmer", ProductType.Wall, FurnitureCategory.Default),
            logics["dimmer_basic"]
        );
        Assert.Equal(("stickie", ProductType.Wall, FurnitureCategory.PostIt), logics["post_it"]);
        Assert.Equal(
            ("present", ProductType.Floor, FurnitureCategory.Present),
            logics["present_gen"]
        );
        Assert.Equal("wf_act_toggle_state", logics["wf_act_toggle_state"].Logic);
        Assert.Equal(
            ("song_disk", ProductType.Floor, FurnitureCategory.TraxSong),
            logics["song_disk"]
        );
        // An effect is no furniture.
        Assert.False(logics.ContainsKey("fx_badge"));
    }

    [Fact]
    public async Task Items_stand_where_they_stood_with_state_their_logic_reads()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        var report = await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var items = await db.Furnitures.ToDictionaryAsync(x => x.Id, ct);

        var throne = items[1000];

        Assert.Equal(
            (100, 1, 1, 0.5, Rotation.East),
            (throne.RoomEntityId, throne.X, throne.Y, throne.Z, throne.Rotation)
        );
        Assert.Equal("0", Extra(throne)["stuff"]!["Data"]!.GetValue<string>());

        // Every stored state is a JSON object, or Turbo would refuse the room.
        Assert.All(items.Values.Where(x => x.ExtraData is not null), x =>
            Assert.IsType<JsonObject>(JsonNode.Parse(x.ExtraData!))
        );

        // A wall item: :w=3,2 l=10,40 r.
        var poster = items[1003];

        Assert.Equal(
            (3, 2, 10, 40.0, Rotation.West),
            (poster.X, poster.Y, poster.WallOffset, poster.Z, poster.Rotation)
        );
        Assert.Equal("12", Extra(poster)["stuff"]!["Data"]!.GetValue<string>());

        // Teleporters know each other, the other half in an inventory.
        Assert.Equal(1002, Section<RoomLinkerData>(items[1001], RoomLinkerData.SECTION).ItemId);
        Assert.Equal(1001, Section<RoomLinkerData>(items[1002], RoomLinkerData.SECTION).ItemId);

        // Trophy date as the client writes it.
        Assert.Equal(
            "Alice\t05-03-2024\tWell done",
            Extra(items[1004])["stuff"]!["Data"]!.GetValue<string>()
        );

        // The dimmer takes the room's presets, Morningstar's per-room store.
        var dimmer = Extra(items[1005]);

        Assert.Equal(Rotation.South, items[1005].Rotation);
        Assert.Equal("2,1,2,#000000,255", dimmer["stuff"]!["Data"]!.GetValue<string>());
        var presets = Section<List<DimmerPresetSnapshot>>(
            items[1005],
            DimmerPresetSnapshot.SECTION
        );

        Assert.Equal(3, presets.Count);
        Assert.Equal(("#123456", 80), (presets[1].Color, presets[1].Brightness));

        Assert.Equal("9CCEFF Hello", Extra(items[1006])["stuff"]!["Data"]!.GetValue<string>());

        // LTD #37 of 100.
        var lamp = Extra(items[1013])["stuff"]!;

        Assert.Equal(
            (37, 100),
            (lamp["UniqueNumber"]!.GetValue<int>(), lamp["UniqueSeries"]!.GetValue<int>())
        );

        // A room not copied sends its furni home; a deleted owner's furni stays with the room's owner.
        Assert.Null(items[1014].RoomEntityId);
        Assert.Equal(11, items[1014].PlayerEntityId);
        Assert.Equal((10, 100), (items[1015].PlayerEntityId, items[1015].RoomEntityId));

        // A marketplace listing goes back to its seller; an unknown base stays behind.
        Assert.Equal((11, (int?)null), (items[1016].PlayerEntityId, items[1016].RoomEntityId));
        Assert.False(items.ContainsKey(1017));
        Assert.Equal(1, report.NotesOf("item left out: its item base was not copied"));
    }

    [Fact]
    public async Task Presents_hold_what_they_wrap_and_jukeboxes_their_discs()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var present = await db.Furnitures.SingleAsync(x => x.Id == 1008, ct);
        var card = Extra(present);

        Assert.Equal(11, present.PlayerEntityId);
        Assert.Equal("Enjoy", card["stuff"]!["Data"]!["MESSAGE"]!.GetValue<string>());
        Assert.Equal("throne", card["stuff"]!["Data"]!["PRODUCT_CODE"]!.GetValue<string>());
        Assert.Equal("Alice", card["stuff"]!["Data"]!["PURCHASER_NAME"]!.GetValue<string>());
        var storage = Section<PresentStorage>(present, PresentStorage.SECTION);

        Assert.Equal((3, 5), (storage.BoxType, storage.RibbonType));

        // The wrapped throne is the receiver's, held by the present, in no inventory or room.
        var wrapped = await db.Furnitures.SingleAsync(x => x.Id == 1007, ct);

        Assert.Equal(
            (1008, 11, (int?)null),
            (wrapped.ChestItemEntityId, wrapped.PlayerEntityId, wrapped.RoomEntityId)
        );

        // The disc is in its jukebox's playlist and plays Turbo's copy of the song.
        var disc = await db.Furnitures.SingleAsync(x => x.Id == 1012, ct);
        var song = await db.Songs.SingleAsync(ct);

        Assert.Equal((1011, 0), (disc.ChestItemEntityId, disc.HeldPosition));
        Assert.Equal("my_song", song.Code);
        Assert.Equal(song.Id.ToString(), Extra(disc)["stuff"]!["Data"]!.GetValue<string>());
    }

    [Fact]
    public async Task Wired_boxes_keep_their_picks_delay_and_snapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var toggle = Extra(await db.Furnitures.SingleAsync(x => x.Id == 1009, ct))["wired"]!;

        Assert.Equal([1000, 1003], toggle["StuffIds"]!.AsArray().Select(x => x!.GetValue<int>()));
        Assert.Equal(4, toggle["DefinitionSpecifics"]![0]!.GetValue<int>());
        Assert.Equal([0], toggle["IntParams"]!.AsArray().Select(x => x!.GetValue<int>()));

        var match = Extra(await db.Furnitures.SingleAsync(x => x.Id == 1010, ct));

        Assert.Equal(
            [1, 1, 0, 0],
            match["wired"]!["IntParams"]!.AsArray().Select(x => x!.GetValue<int>())
        );

        // What the throne is to be put back to, with Turbo's height and definition.
        var remembered = match["wired_snapshot"]!["1000"]!;

        Assert.Equal(1, remembered["state"]!.GetValue<int>());
        Assert.Equal(4, remembered["rotation"]!.GetValue<int>());
        Assert.Equal(50, remembered["z"]!.GetValue<int>());
        Assert.Equal(THRONE, remembered["definitionId"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_game_timer_turbo_knows_by_its_classname_keeps_its_time()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        // Turbo's own counter is plain furniture by its logic column; the room makes it a game
        // timer by its classname, and its state is the timer's seconds.
        target.Insert(
            new FurnitureDefinitionEntity
            {
                SpriteId = 231,
                Name = "bb_counter",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                Logic = DefaultFurnitureLogic.FLOOR,
                TotalStates = 1,
                Width = 1,
                Length = 1,
                StackHeight = 1,
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = false,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
            }
        );
        Base(ms, 14, "bb_counter", "s", "game_timer");
        // Morningstar: the time left, then the time it is reset to.
        Item(ms, 1018, 10, 100, 14, x: 3, y: 1, extra: "45	120");

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        Assert.Equal(
            "120",
            Extra(await db.Furnitures.SingleAsync(x => x.Id == 1018, ct))["stuff"]![
                "Data"
            ]!.GetValue<string>()
        );
    }

    [Fact]
    public async Task A_snapshot_remembers_a_height_as_the_room_reads_one()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        // 0.29 is 28.999... hundredths in a double: the room truncates it to 28.
        Item(ms, 1018, 10, 100, 1, x: 2, y: 2, z: 0.29);
        Item(
            ms,
            1019,
            10,
            100,
            9,
            x: 1,
            y: 2,
            wired: """{"state":true,"direction":true,"position":true,"items":[{"item_id":1018,"state":"0","rotation":0,"x":2,"y":2}],"delay":0}"""
        );

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var throne = await db.Furnitures.SingleAsync(x => x.Id == 1018, ct);
        var remembered = Extra(await db.Furnitures.SingleAsync(x => x.Id == 1019, ct))[
            "wired_snapshot"
        ]!["1018"]!;

        Assert.Equal(Altitude.FromValue(throne.Z).ToInt(), remembered["z"]!.GetValue<int>());
    }
}
