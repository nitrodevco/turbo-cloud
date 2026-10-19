using System.Collections;
using FluentAssertions;
using Turbo.Database.Entities.Furniture;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Object.Logic.Furniture.Wall;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Post-it pads. A pad's legacy stuff data is its sheets left (Flash <c>GroupItem.getTotalCount</c>
/// reads <c>int(legacy data)</c> for category 5); each sheet stuck to a wall is a new note of the
/// pad's own kind, belonging to the room's owner, and the pad is told its new count
/// (<c>PostItPlaced</c>, Flash <c>FurniModel.updatePostItCount</c>) or, with its last sheet, leaves
/// the list. Without building rights a note goes up only on a room with a post-it wall: the
/// placement is answered with the note editor for that pad and spot (<c>RequestSpamWallPostIt</c>,
/// whose id Flash's <c>SpamWallPostItWidgetHandler</c> looks up among the inventory's wall
/// items), and the note comes back as <c>AddSpamWallPostIt</c>. Turbo's post-it wall used to hand
/// out free notes on a double-click, under its own id.
/// </summary>
public sealed class PostItPadTests : IDisposable
{
    // The room harness is room 1, owned by player 1; player 102 stands in it with rights.
    private const int OWNER = 1;
    private const int BUILDER = 102;
    private const int GUEST = 9;
    private const int ROOM = 1;
    private const int PAD_KIND = 40;
    private const int THEMED_KIND = 41;
    private const int PAD = 500;
    private const int POST_IT_WALL = 70;
    private const string SPOT = ":w=2,3 l=10,20 r";

    private readonly FurnitureWorld _world = new();
    private readonly WiredRoom _room = new(
        8,
        8,
        new RoomConfig { RightsHoldersCanPlaceFurni = true }
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PostItPadTests()
    {
        _world.AddPlayer(OWNER, "owner");
        _world.AddPlayer(BUILDER, "builder");
        _world.AddPlayer(GUEST, "guest");
        _world.AddDefinition(
            PAD_KIND,
            "post_it",
            "stickie",
            ProductType.Wall,
            FurnitureCategory.PostIt
        );
        _world.AddDefinition(
            THEMED_KIND,
            "post_it_vd",
            "stickie",
            ProductType.Wall,
            FurnitureCategory.PostIt
        );
        _world.AddRoom(ROOM, OWNER);

        _room.Enter(2, 1, 1);
        ((ISet<PlayerId>)RoomHarness.GetMember(_room.Harness.State, "PlayerIdsWithRights")!).Add(
            BUILDER
        );

        // What the room's item provider makes of a snapshot: the item, its logic given on attach.
        _room.Harness.Fakes.Handlers["CreateFromFurnitureItemSnapshot"] = call =>
        {
            var snapshot = (FurnitureItemSnapshot)call.Args[0]!;
            var item = new RoomWallItem
            {
                ObjectId = snapshot.ItemId,
                OwnerId = snapshot.OwnerId,
                OwnerName = snapshot.OwnerName,
                Definition = snapshot.Definition,
            };
            item.SetExtraData(snapshot.ExtraData);

            return item;
        };
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task A_builder_sticks_a_sheet_and_the_pad_keeps_the_rest()
    {
        await HoldsPadAsync(BUILDER, PAD_KIND, PostItPads.ExtraData(3));
        await UseTheDatabaseAsync();

        (await PlaceAsync(BUILDER)).Should().Be(FurniturePlacementResultType.Done);

        var note = Notes().Should().ContainSingle().Subject;
        (note.Definition.Id, note.OwnerId, note.X, note.Y)
            .Should()
            .Be((PAD_KIND, (PlayerId)OWNER, 2, 3), "a note is the room owner's");
        NoteData(note).Should().Be(StickieColors.DEFAULT, "a fresh note is yellow and empty");
        _world.Row(note.ObjectId.Value).RoomEntityId.Should().Be(ROOM);

        _world.Row(PAD).ExtraData.Should().Be(PostItPads.ExtraData(2));
        (await SheetsHeldAsync(BUILDER, PAD)).Should().Be(2);
        ToldInventory(BUILDER)
            .Should()
            .Equal(new PostItPlacedEventMessageComposer { ItemId = PAD, ItemsLeft = 2 });

        var made = _world.Movements().Should().ContainSingle().Subject;
        (made.Cause, made.CauseRef, made.Actor, made.FromKind, made.ToKind, made.ToOwner)
            .Should()
            .Be(
                (
                    MovementCauses.ROOM_POST_IT,
                    PAD.ToString(),
                    BUILDER,
                    PlaceKind.None,
                    PlaceKind.Room,
                    OWNER
                )
            );
    }

    [Fact]
    public async Task A_themed_pad_makes_notes_of_its_own_kind()
    {
        await HoldsPadAsync(BUILDER, THEMED_KIND, PostItPads.ExtraData(5));
        await UseTheDatabaseAsync();

        (await PlaceAsync(BUILDER)).Should().Be(FurniturePlacementResultType.Done);

        Notes().Should().ContainSingle().Which.Definition.Name.Should().Be("post_it_vd");
    }

    [Theory]
    [InlineData("{\"stuff\":{\"Data\":\"1\"}}")]
    [InlineData("{\"stuff\":{\"Data\":\"\"}}")]
    [InlineData("{\"stuff\":{\"Data\":\"0\"}}")]
    [InlineData(null)]
    public async Task The_last_sheet_is_the_pad_itself_and_it_leaves_the_inventory(string? padData)
    {
        await HoldsPadAsync(BUILDER, PAD_KIND, padData);
        await UseTheDatabaseAsync();

        (await PlaceAsync(BUILDER)).Should().Be(FurniturePlacementResultType.Done);

        var note = Notes().Should().ContainSingle().Subject;
        (note.Definition.Id, note.OwnerId).Should().Be((PAD_KIND, (PlayerId)OWNER));
        NoteData(note).Should().Be(StickieColors.DEFAULT, "the count is not a note's data");

        _world.Rows().Select(x => x.Id).Should().Equal(note.ObjectId.Value);
        (await HeldAsync(BUILDER)).Should().BeEmpty();
        ToldInventory(BUILDER)
            .Should()
            .Equal(new FurniListRemoveEventMessageComposer { ItemId = PAD });
        _world
            .Movements()
            .Select(x => (x.Seq, x.AssetId == PAD, x.FromKind, x.ToKind))
            .Should()
            .Equal(
                ((short)0, true, PlaceKind.Inventory, PlaceKind.None),
                ((short)1, false, PlaceKind.None, PlaceKind.Room)
            );
    }

    [Fact]
    public async Task A_note_that_cannot_be_made_gives_the_sheet_back()
    {
        await HoldsPadAsync(BUILDER, PAD_KIND, PostItPads.ExtraData(3));
        CannotMakeNotes();

        (await PlaceAsync(BUILDER)).Should().Be(FurniturePlacementResultType.Refused);

        Notes().Should().BeEmpty();
        _world.Row(PAD).ExtraData.Should().Be(PostItPads.ExtraData(3));
        (await SheetsHeldAsync(BUILDER, PAD)).Should().Be(3);
        ToldInventory(BUILDER)
            .Cast<PostItPlacedEventMessageComposer>()
            .Select(x => x.ItemsLeft)
            .Should()
            .Equal(2, 3);
    }

    [Fact]
    public async Task A_last_sheet_no_note_came_of_comes_back_as_a_pad_of_one()
    {
        await HoldsPadAsync(BUILDER, THEMED_KIND, PostItPads.ExtraData(1));
        CannotMakeNotes();

        (await PlaceAsync(BUILDER)).Should().Be(FurniturePlacementResultType.Refused);

        var pad = (await HeldAsync(BUILDER)).Should().ContainSingle().Subject;
        (pad.Definition.Id, PostItPads.SheetsOf(pad.StuffData)).Should().Be((THEMED_KIND, 1));
        var given = _world.Movements().Last();
        (given.FromKind, given.ToKind, given.ToOwner, given.Cause)
            .Should()
            .Be((PlaceKind.None, PlaceKind.Inventory, BUILDER, MovementCauses.ROOM_POST_IT));
    }

    [Fact]
    public async Task Without_rights_in_a_room_with_a_post_it_wall_the_note_editor_opens_on_the_pad()
    {
        await HoldsPadAsync(GUEST, PAD_KIND, PostItPads.ExtraData(3));
        AddPostItWall();

        (await PlaceAsync(GUEST)).Should().Be(FurniturePlacementResultType.Done);

        ToldByRoom(GUEST)
            .Should()
            .Equal(new RequestSpamWallPostItMessageComposer { ItemId = PAD, Location = SPOT });
        Notes().Should().BeEmpty("the note comes with the editor's answer");
        (await SheetsHeldAsync(GUEST, PAD)).Should().Be(3);
    }

    [Fact]
    public async Task Without_rights_and_without_a_post_it_wall_the_sheet_is_refused()
    {
        await HoldsPadAsync(GUEST, PAD_KIND, PostItPads.ExtraData(3));

        (await PlaceAsync(GUEST)).Should().Be(FurniturePlacementResultType.NotAllowed);

        ToldByRoom(GUEST).Should().BeEmpty();
        Notes().Should().BeEmpty();
        (await SheetsHeldAsync(GUEST, PAD)).Should().Be(3);
    }

    [Fact]
    public async Task The_editors_answer_sticks_a_sheet_in_its_colour_with_its_text()
    {
        await HoldsPadAsync(GUEST, PAD_KIND, PostItPads.ExtraData(3));
        await UseTheDatabaseAsync();
        AddPostItWall();

        (await AddSpamWallPostItAsync(GUEST, "9cceff", "hello")).Should().BeTrue();

        var note = Notes().Should().ContainSingle().Subject;
        (note.Definition.Id, note.OwnerId, note.X, note.Y)
            .Should()
            .Be((PAD_KIND, (PlayerId)OWNER, 2, 3));
        NoteData(note).Should().Be("9CCEFF hello");
        (await SheetsHeldAsync(GUEST, PAD)).Should().Be(2);
        ToldInventory(GUEST)
            .Should()
            .Equal(new PostItPlacedEventMessageComposer { ItemId = PAD, ItemsLeft = 2 });
    }

    [Fact]
    public async Task The_editors_answer_with_the_last_sheet_uses_the_pad_up()
    {
        await HoldsPadAsync(GUEST, PAD_KIND, null);
        await UseTheDatabaseAsync();
        AddPostItWall();

        (await AddSpamWallPostItAsync(GUEST, "FFFF33", "last")).Should().BeTrue();

        Notes().Should().ContainSingle();
        (await HeldAsync(GUEST)).Should().BeEmpty();
        ToldInventory(GUEST)
            .Should()
            .Equal(new FurniListRemoveEventMessageComposer { ItemId = PAD });
    }

    [Fact]
    public async Task The_editors_answer_is_refused_without_a_post_it_wall_or_with_a_bad_colour()
    {
        await HoldsPadAsync(GUEST, PAD_KIND, PostItPads.ExtraData(3));
        await UseTheDatabaseAsync();

        (await AddSpamWallPostItAsync(GUEST, "FFFF33", "hi")).Should().BeFalse();

        AddPostItWall();

        (await AddSpamWallPostItAsync(GUEST, "123456", "hi")).Should().BeFalse();
        (await AddSpamWallPostItAsync(GUEST, "FFFF33", "")).Should().BeFalse();

        Notes().Should().BeEmpty();
        (await SheetsHeldAsync(GUEST, PAD)).Should().Be(3);
    }

    [Fact]
    public async Task The_editors_answer_naming_no_pad_of_theirs_makes_nothing()
    {
        await HoldsPadAsync(OWNER, PAD_KIND, PostItPads.ExtraData(3));
        await UseTheDatabaseAsync();
        AddPostItWall();
        _room.Harness.Fakes.Instances[(typeof(IInventoryGrain), (long)GUEST)] =
            await _world.NewInventoryAsync(GUEST, Ct);

        (await AddSpamWallPostItAsync(GUEST, "FFFF33", "free note")).Should().BeFalse();

        Notes().Should().BeEmpty();
        (await SheetsHeldAsync(OWNER, PAD)).Should().Be(3);
    }

    [Fact]
    public async Task Double_clicking_a_post_it_wall_hands_out_no_note()
    {
        var wall = AddPostItWall();

        await wall.Logic.OnUseAsync(ActionContext.CreateForPlayer(GUEST, ROOM), 0, Ct);

        _room
            .Harness.Fakes.Log.Calls.Select(x => x.Args.FirstOrDefault())
            .OfType<RequestSpamWallPostItMessageComposer>()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task The_editors_answer_reaches_the_room_naming_the_pad_not_a_room_item()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("AddSpamWallPostItMessageEvent"),
            PacketHarness.Payload(w => w.Int(PAD).String(SPOT).String("9CCEFF").String("hi")),
            playerId: GUEST,
            roomId: 5
        );

        var call = harness.Fakes.Log.Of("AddSpamWallPostItAsync").Should().ContainSingle().Subject;
        call.Key.Should().Be(5L);
        call.Args.Take(5).Skip(1).Should().Equal((RoomObjectId)PAD, SPOT, "9CCEFF", "hi");
        harness.Fakes.Log.Of("InteractWithItemAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task A_placed_pad_goes_to_the_room_as_a_post_it()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("PlacePostItMessageEvent"),
            PacketHarness.Payload(w => w.Int(PAD).String(SPOT)),
            playerId: GUEST,
            roomId: 5
        );

        var call = harness.Fakes.Log.Of("PlacePostItAsync").Should().ContainSingle().Subject;
        call.Key.Should().Be(5L);
        call.Args.Take(3).Skip(1).Should().Equal((RoomObjectId)PAD, SPOT);
        harness.Fakes.Log.Of("PlaceWallItemAsync").Should().BeEmpty();
    }

    private Task<FurniturePlacementResultType> PlaceAsync(int playerId) =>
        _room.Harness.Room.PlacePostItAsync(
            ActionContext.CreateForPlayer(playerId, ROOM),
            PAD,
            SPOT,
            Ct
        );

    private Task<bool> AddSpamWallPostItAsync(int playerId, string color, string text) =>
        _room.Harness.Room.AddSpamWallPostItAsync(
            ActionContext.CreateForPlayer(playerId, ROOM),
            PAD,
            SPOT,
            color,
            text,
            Ct
        );

    /// <summary>A pad of <paramref name="kind"/> in the player's inventory, the room reaching that inventory.</summary>
    private async Task HoldsPadAsync(int playerId, int kind, string? extraData)
    {
        _world.Db.Insert(
            new FurnitureEntity
            {
                Id = PAD,
                PlayerEntityId = playerId,
                FurnitureDefinitionEntityId = kind,
                RoomEntityId = null,
                ExtraData = extraData,
            }
        );

        _room.Harness.Fakes.Instances[(typeof(IInventoryGrain), (long)playerId)] =
            await _world.NewInventoryAsync(playerId, Ct);
    }

    /// <summary>The room makes its notes through the custody over the test database, recording.</summary>
    private async Task UseTheDatabaseAsync() =>
        RoomHarness.SetField(
            _room.Harness.Room,
            "_custody",
            TestCustody.Over(_world.Db, await _world.NewRecorderAsync(Ct))
        );

    /// <summary>The note's row cannot be written: the database went away.</summary>
    private void CannotMakeNotes() =>
        _room.Harness.Fakes.Handlers["CreateInRoomAsync"] = _ =>
            Task.FromException<IReadOnlyList<CustodyFurni>>(
                new InvalidOperationException("the database is down")
            );

    private RoomWallItem AddPostItWall()
    {
        var wall = _room.AddWallItem(POST_IT_WALL);

        wall.SetLogic(
            new FurnitureSpamWallLogic(
                _room.StuffData,
                new RoomWallItemContext(_room.Harness.Room, wall)
            )
        );

        return wall;
    }

    /// <summary>Every wall item in the room but the post-it wall.</summary>
    private List<IRoomItem> Notes() =>
        [
            .. ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!)
                .Values.Cast<IRoomItem>()
                .Where(x =>
                    x.ObjectId != POST_IT_WALL && x.Definition.ProductType == ProductType.Wall
                ),
        ];

    private static string NoteData(IRoomItem note) =>
        ((LegacyStuffSnapshot)note.GetSnapshot().StuffData).Data;

    private async Task<IReadOnlyList<FurnitureItemSnapshot>> HeldAsync(int playerId) =>
        await (
            (IInventoryGrain)
                _room.Harness.Fakes.Instances[(typeof(IInventoryGrain), (long)playerId)]
        ).GetAllItemSnapshotsAsync(Ct);

    private async Task<int> SheetsHeldAsync(int playerId, int padId) =>
        PostItPads.SheetsOf((await HeldAsync(playerId)).Single(x => x.ItemId == padId).StuffData);

    /// <summary>What the player's inventory told their client, in order.</summary>
    private IEnumerable<object> ToldInventory(int playerId) =>
        _world
            .Fakes.Log.Calls.Where(x =>
                x.Interface == typeof(IPlayerPresenceGrain)
                && x.Method == "SendComposerAsync"
                && Equals(x.Key, (long)playerId)
            )
            .SelectMany(x => x.Args[0] is IEnumerable many ? many.Cast<object>() : [x.Args[0]!]);

    /// <summary>What the room told the player directly.</summary>
    private IEnumerable<object> ToldByRoom(int playerId) =>
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Interface == typeof(IPlayerPresenceGrain)
                && x.Method == "SendComposerAsync"
                && Equals(x.Key, (long)playerId)
            )
            .Select(x => x.Args[0]!)
            .OfType<RequestSpamWallPostItMessageComposer>();
}
