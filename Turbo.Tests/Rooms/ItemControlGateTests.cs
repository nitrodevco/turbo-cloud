using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Avatars.Pet;
using Turbo.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Wall;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Who may change or take what a room holds, against the gate Flash puts on each, build
/// WIN63-202610201032: a player whose client never offers the action is refused, and one whose
/// client offers it is not. What Habbo's server itself refuses is not recorded for these: the rule
/// is the client's.
/// </summary>
[Trait("evidence", "inferred")]
public sealed class ItemControlGateTests
{
    // The room is player 1's; player 102 has rights in it, player 103 is a guest without.
    private const int OWNER = 1;
    private const int BUILDER = 2;
    private const int GUEST = 3;
    private const int NOTE = 600;
    private const int POSTER = 601;
    private const int DISPLAY = 602;
    private const int MANNEQUIN = 603;
    private const int HELPER = 604;
    private const int BOT = 70;
    private const int PET = 80;

    private readonly WiredRoom _room = new(
        8,
        8,
        new RoomConfig
        {
            YoutubePlaylists =
            [
                new YoutubePlaylistConfig
                {
                    Id = "music",
                    Videos = [new YoutubeVideoConfig { VideoId = "v", DurationSeconds = 60 }],
                },
            ],
        }
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ItemControlGateTests()
    {
        _room.Enter(OWNER, 1, 1, playerId: 1);
        _room.Enter(BUILDER, 2, 1);
        _room.Enter(GUEST, 3, 1);
        _room.Harness.State.PlayerIdsWithRights.Add(100 + BUILDER);
    }

    private ActionContext As(int objectId) => _room.ContextFor(objectId);

    // SetItemData wrote any item's state, so rights let a player rewrite the owner's furni.
    // Flash sends it from the post-it's widget alone (FurnitureStickieWidgetHandler.as:95).
    [Fact]
    public async Task Item_data_writes_a_note_only()
    {
        var poster = _room.AddWallItem(POSTER);
        await poster.Logic.SetStateAsync(1);

        (await _room.Harness.Room.SetItemDataAsync(As(OWNER), POSTER, "FFFF33", "hi", Ct))
            .Should()
            .BeFalse();
        poster.Logic.GetLegacyString().Should().Be("1");
    }

    // The colour buttons are the room owner's and staff's (StickieFurniWidget.as:177,
    // FurnitureStickieWidgetHandler.as:81); a rights holder's client sends the colour back as it is.
    [Fact]
    public async Task A_rights_holder_writes_a_notes_text_but_not_its_colour()
    {
        var note = await AddNoteAsync("9CCEFF old");

        (await _room.Harness.Room.SetItemDataAsync(As(BUILDER), NOTE, "FF9C9C", "new", Ct))
            .Should()
            .BeFalse();
        note.Logic.GetLegacyString().Should().Be("9CCEFF old");

        (await _room.Harness.Room.SetItemDataAsync(As(BUILDER), NOTE, "9ccEFF", "new", Ct))
            .Should()
            .BeTrue();
        note.Logic.GetLegacyString().Should().Be("9CCEFF new");
    }

    [Fact]
    public async Task The_rooms_owner_changes_a_notes_colour()
    {
        var note = await AddNoteAsync("9CCEFF old");

        (await _room.Harness.Room.SetItemDataAsync(As(OWNER), NOTE, "FF9C9C", "old", Ct))
            .Should()
            .BeTrue();
        note.Logic.GetLegacyString().Should().Be("FF9C9C old");
    }

    // FurnitureYoutubeDisplayWidgetHandler.as:125: the furni's owner or security level 4.
    [Theory]
    [InlineData(OWNER, true)]
    [InlineData(BUILDER, false)]
    public async Task Only_the_displays_owner_picks_its_playlist(int objectId, bool picks)
    {
        _room.AddFloorItem(
            DISPLAY,
            5,
            5,
            "youtube",
            createLogic: (factory, ctx) => new FurnitureYoutubeLogic(factory, ctx)
        );

        (
            await _room.Harness.Room.InteractWithItemAsync(
                As(objectId),
                DISPLAY,
                new SetYoutubePlaylistInteraction { PlaylistId = "music" },
                Ct
            )
        )
            .Should()
            .Be(picks);
    }

    // MannequinWidget.as:129: its editing view for the room's owner, level 1 and up and staff.
    [Theory]
    [InlineData(BUILDER, true)]
    [InlineData(GUEST, false)]
    public async Task Rights_name_the_owners_mannequin(int objectId, bool names)
    {
        _room.AddFloorItem(
            MANNEQUIN,
            5,
            5,
            "mannequin",
            createLogic: (factory, ctx) => new FurnitureMannequinLogic(factory, ctx)
        );

        (
            await _room.Harness.Room.InteractWithItemAsync(
                As(objectId),
                MANNEQUIN,
                new SetMannequinNameInteraction { Name = "Look" },
                Ct
            )
        )
            .Should()
            .Be(names);
    }

    // FurnitureCustomStackHeightWidgetHandler.as:143-150: rights, or the helper's own owner.
    [Fact]
    public async Task A_guest_sets_the_height_of_their_own_stacking_helper()
    {
        var helper = _room.AddFloorItem(
            HELPER,
            5,
            5,
            "custom_stack_height",
            createLogic: (factory, ctx) => new FurnitureStackTileLogic(factory, ctx)
        );
        helper.SetOwnerId(100 + GUEST);

        (
            await _room.Harness.Room.InteractWithItemAsync(
                As(GUEST),
                HELPER,
                new SetStackHeightInteraction { Height = 0 },
                Ct
            )
        )
            .Should()
            .BeTrue();
    }

    // RentableBotMenuView.as:67-68: Pick up for the room's owner and staff only.
    [Theory]
    [InlineData(OWNER, true)]
    [InlineData(BUILDER, false)]
    public async Task Only_the_rooms_owner_picks_up_its_bot(int objectId, bool picks)
    {
        AddBot();

        (await _room.Harness.Room.PickupBotAsync(As(objectId), BOT, Ct)).Should().Be(picks);
    }

    // PetMenuView.as:101-104: Pick up on another's pet for the room's owner, level 1 and staff.
    [Fact]
    public async Task Rights_pick_up_a_guests_pet()
    {
        AddPet(owner: 100 + GUEST, typeId: 0);
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnPetAsync)] = _ =>
            Task.FromResult(true);

        (await Pets.PickupPetAsync(As(BUILDER), PET, Ct)).Should().BeTrue();
    }

    // OwnPetMenuView.as:214: Compost on the own plant's menu for the room's owner alone.
    [Fact]
    public async Task A_guest_does_not_compost_their_plant_in_someone_elses_room()
    {
        AddPet(owner: 100 + GUEST, typeId: PetTypes.MONSTERPLANT);
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.DeletePetAsync)] = _ =>
            Task.FromResult(true);

        (await Pets.CompostPetAsync(As(GUEST), PET, Ct)).Should().BeFalse();
        Pets.TryGetPet(PET, out _).Should().BeTrue();
    }

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    private async Task<RoomWallItem> AddNoteAsync(string data)
    {
        var note = _room.AddWallItem(NOTE);

        note.SetLogic(
            new FurnitureStickieLogic(
                _room.StuffData,
                new RoomWallItemContext(_room.Harness.Room, note)
            )
        );
        await note.Logic.SetLegacyDataAsync(data);

        return note;
    }

    private void AddBot()
    {
        var bot = new RoomBotAvatar
        {
            ObjectId = BOT,
            BotId = BOT,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = 1,
            Skills = [],
        };

        bot.SetName("Frank");
        bot.SetPosition(4, 4);
        bot.SetLogic(_room.Harness.Fakes.Create<IRoomBotLogic>(BOT));
        _room.Harness.State.AvatarsByObjectId[BOT] = bot;
        _room.Harness.State.AvatarsByBotId[BOT] = BOT;
        _room.Harness.State.Bots.Add(bot);
        _room.Map.AddAvatar(bot, false);
    }

    private void AddPet(int owner, int typeId)
    {
        RoomObjectId objectId = PET;
        var pet = RoomPetAvatar.FromSnapshot(
            objectId,
            new PetSnapshot
            {
                Id = PET,
                OwnerId = owner,
                OwnerName = "guest",
                RoomId = null,
                Name = "Rex",
                Figure = new PetFigureSnapshot
                {
                    TypeId = typeId,
                    PaletteId = 1,
                    Color = "FFFFFF",
                    BreedId = 0,
                    CustomParts = [],
                },
                Level = 7,
                Experience = 0,
                Energy = 100,
                Nutrition = 100,
                Respect = 0,
                RarityLevel = 0,
                HasSaddle = false,
                AnyoneCanRide = false,
                HasBreedingPermission = false,
                X = 4,
                Y = 4,
                Z = Altitude.Zero,
                Rotation = Rotation.South,
                CreatedAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                // Long dry: a dead plant, the only kind the client offers to compost.
                WateredAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HarvestedAtUtc = null,
            }
        );

        pet.SetLogic(_room.Harness.Fakes.Create<IRoomPetLogic>(PET));
        _room.Harness.State.AvatarsByObjectId[objectId] = pet;
        _room.Harness.State.AvatarsByPetId[PET] = objectId;
        _room.Harness.State.Pets.Add(pet);
        _room.Map.AddAvatar(pet, false);
    }
}
