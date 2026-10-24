using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Place Temporary Furni" saved the way its editor saves it (<c>_-U1h</c>: custom target is a
/// user, location, altitude, offsets x / y / altitude, spawn with variable, value option, value,
/// value target): a click puts a copy of the lamp, as it stood when the box was saved, into the
/// room.
/// </summary>
public sealed class WiredPlaceFurniTests
{
    private const int CLICK_ME = 23;

    // Habbo copies only Builders Club furni (wiredfurni.error.furni_can_not_be_cloned).
    private const int LAMP = FurniIdBands.BuildersClubMin + 20;
    private const int OWN_LAMP = 30;
    private const int BORROWED_CHAIR = FurniIdBands.BuildersClubMin + 21;
    private const int OWN_MONEY_BAG = 31;
    private const int BORROWED_MONEY_BAG = FurniIdBands.BuildersClubMin + 31;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomFurniModule Furni => _room.Harness.Module<RoomFurniModule>();

    [Theory]
    [InlineData(0, 0, 3, 3, false, false)]
    [InlineData(2, -1, 5, 2, false, false)]
    [InlineData(0, 0, 3, 3, true, false)]
    // "Use picked furnis" instead of the default "Use the snapshot" (official client capture,
    // slot 0 [0,100,101,110,200,201] default 110): the lamp as it stands, no snapshot needed.
    [InlineData(0, 0, 3, 3, false, true)]
    public async Task A_click_places_a_temporary_copy_of_the_lamp(
        int offsetX,
        int offsetY,
        int x,
        int y,
        bool snapshotWithoutType,
        bool pickedAsTheyStand
    )
    {
        _room.Enter(5, 7, 7);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        var lamp = _room.AddFloorItem(LAMP, 3, 3);

        _room.Harness.Fakes.Handlers["TryGetDefinition"] = call =>
            call.Args[0] is int id && id == lamp.Definition.Id ? lamp.Definition : null;
        _room.Harness.Fakes.Handlers["CreateFloorItem"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                OwnerId = (PlayerId)call.Args[1]!,
                OwnerName = string.Empty,
                Definition = (FurnitureDefinitionSnapshot)call.Args[2]!,
            };

            item.SetExtraData(null);

            return item;
        };

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionPlaceFurni>(2, 0, 0, "wf_act_place_furni");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [0, 0, 0, offsetX, offsetY, 0, 0, 0, 0, 0],
                stuffIds: [LAMP],
                furniSources: pickedAsTheyStand
                    ?
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                    : null,
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        if (pickedAsTheyStand)
            _room.FloorItem(2).ExtraData.DeleteSection(WiredFurniSnapshotEntry.SECTION);

        // A box saved before its snapshot kept each furni's type (before 20/09/2026).
        if (snapshotWithoutType)
            _room
                .FloorItem(2)
                .ExtraData.UpdateSection(
                    WiredFurniSnapshotEntry.SECTION,
                    new Dictionary<int, WiredFurniSnapshotEntry> { [LAMP] = new(0, 0, 3, 3, 0) }
                );

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);

        var copies = Furni.Items.Where(x => x.IsTemporary).ToList();

        copies.Should().ContainSingle();
        (copies[0].X, copies[0].Y, copies[0].Definition.Id).Should().Be((x, y, lamp.Definition.Id));
    }

    // Official client capture (WIN63-202610131318, owner's room): a run whose copies cannot be
    // placed sends one "wired.error" dialog however many failed, and none again within ~5 s.
    [Fact]
    public async Task A_run_whose_copies_cannot_be_placed_tells_the_clicking_user_once_per_run_and_not_again_within_5_s()
    {
        await SetUpFailingPlacementAsync();

        await ClickAsync(105);
        Dialogs().Should().Equal(105);
        Dialogs(x => x)
            .Single()
            .Should()
            .Match<NotificationDialogMessageComposer>(x =>
                x.NotificationType == "wired.error"
                && x.Parameters["message"] == "${room.error.wired.placement_failure}"
                && x.Parameters["image"] == "${image.library.url}album1584/WIRD2.png"
            );

        await ClickAsync(105);
        Dialogs().Should().Equal([105], "the same user is not told again within 5 s");

        // The room's clock is the real one: the dialog is made to have been 6 s ago.
        var notified = _room.Harness.State.WiredPlacementFailureNotifiedMs;
        notified[(PlayerId)105] -= 6_000;

        await ClickAsync(105);
        Dialogs().Should().Equal(105, 105);
    }

    // Inference (only the owner's own runs were recorded): the cooldown is each user's own, so
    // another user whose click fails within it is still told, and only that user.
    [Fact]
    public async Task Another_user_whose_run_fails_within_the_cooldown_is_told_too()
    {
        await SetUpFailingPlacementAsync();
        _room.Enter(6, 6, 6);

        await ClickAsync(105);
        await ClickAsync(106);

        Dialogs().Should().Equal(105, 106);
    }

    // Official client capture (WIN63-202610131318): the run that placed its copy got no dialog.
    [Fact]
    public async Task A_run_that_places_its_copies_tells_nobody()
    {
        await SetUpFailingPlacementAsync(offsetX: 1);

        await ClickAsync(105);

        Furni.Items.Count(x => x.IsTemporary).Should().Be(2);
        Dialogs().Should().BeEmpty();
    }

    // Official client capture 2026-10-24 (action.55.R4): the template picked up, the box still
    // lists it in its snapshot text, and a run flashes the box but places nothing and tells
    // nobody. Inference: a template still in the room is copied all the same.
    [Fact]
    public async Task Place_furni_does_not_copy_a_template_that_was_picked_up_action_55_R4()
    {
        await SetUpFailingPlacementAsync(offsetX: 1);

        (
            await _room.Harness.Room.RemoveItemByIdAsync(
                ActionContext.CreateForSystem(1),
                LAMP,
                true,
                Ct
            )
        )
            .Should()
            .BeTrue();

        await ClickAsync(105);

        Furni.Items.Count(x => x.IsTemporary).Should().Be(1);
        Dialogs().Should().BeEmpty();
    }

    [Fact]
    public async Task Place_furni_with_every_template_picked_up_places_nothing_and_tells_nobody_action_55_R4()
    {
        await SetUpFailingPlacementAsync(offsetX: 1);

        foreach (var template in new[] { LAMP, BORROWED_CHAIR })
            (
                await _room.Harness.Room.RemoveItemByIdAsync(
                    ActionContext.CreateForSystem(1),
                    template,
                    true,
                    Ct
                )
            )
                .Should()
                .BeTrue();

        await ClickAsync(105);

        Furni.Items.Should().NotContain(x => x.IsTemporary);
        Dialogs().Should().BeEmpty();
    }

    /// <summary>
    /// A click on <c>CLICK_ME</c> runs Place Temporary Furni over the lamp and a chair of its type,
    /// moved <paramref name="offsetX"/> tiles right: the default ten is off the 8x8 map for both.
    /// </summary>
    private async Task SetUpFailingPlacementAsync(int offsetX = 10)
    {
        _room.Enter(5, 7, 7);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        var lamp = _room.AddFloorItem(LAMP, 3, 3);
        _room.AddFloorItem(BORROWED_CHAIR, 2, 2, definitionId: lamp.Definition.Id);

        _room.Harness.Fakes.Handlers["TryGetDefinition"] = call =>
            call.Args[0] is int id && id == lamp.Definition.Id ? lamp.Definition : null;
        _room.Harness.Fakes.Handlers["CreateFloorItem"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                OwnerId = (PlayerId)call.Args[1]!,
                OwnerName = string.Empty,
                Definition = (FurnitureDefinitionSnapshot)call.Args[2]!,
            };

            item.SetExtraData(null);

            return item;
        };
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionPlaceFurni>(2, 0, 0, "wf_act_place_furni");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [0, 0, 0, offsetX, 0, 0, 0, 0, 0, 0],
                stuffIds: [LAMP, BORROWED_CHAIR],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync(int playerId)
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)playerId, (RoomId)1),
                0,
                Ct
            );
        await TickAsync(4);
    }

    /// <summary>Who was sent a notification dialog, in order.</summary>
    private List<int> Dialogs() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("SendComposerAsync")
                // The placement failure's own: a room this busy on a loaded machine may also be
                // marked heavy, which tells its owner too. Read from the log itself: the answer
                // is whom each dialog went to, which the shared SentToPlayer does not say.
                .Where(x =>
                    x.Args[0] is NotificationDialogMessageComposer dialog
                    && dialog.Parameters["message"] == "${room.error.wired.placement_failure}"
                )
                .Select(x => Convert.ToInt32(x.Key)),
        ];

    // The placement failure's own, as Dialogs(): a loaded machine may also mark the room heavy.
    private List<T> Dialogs<T>(Func<NotificationDialogMessageComposer, T> select) =>
        [
            .. _room
                .Harness.Fakes.Sent<NotificationDialogMessageComposer>(SendRoute.Direct)
                .Where(x =>
                    x.Parameters.TryGetValue("message", out var message)
                    && message == "${room.error.wired.placement_failure}"
                )
                .Select(select),
        ];

    // Official client capture (WIN63-202610131318): the player's own non-tradable chair refused
    // with furni_can_not_be_cloned and the old configuration kept; borrowed furni copied.
    [Theory]
    [InlineData(OWN_LAMP, "default_floor", true)]
    [InlineData(OWN_LAMP, "default_floor", false)]
    [InlineData(OWN_MONEY_BAG, "exchange", true)]
    [InlineData(OWN_MONEY_BAG, "exchange", false)]
    [InlineData(BORROWED_MONEY_BAG, "exchange", true)]
    public async Task A_save_picking_furni_that_may_not_be_copied_is_refused_and_the_old_one_kept(
        int picked,
        string logic,
        bool buildersClubOnly
    )
    {
        _room.Harness.Room.UseWiredConfig(
            new WiredConfig { PlaceFurniBuildersClubOnly = buildersClubOnly }
        );
        _room.AddFloorItem(LAMP, 3, 3);
        _room.AddFloorItem(
            picked,
            4,
            4,
            logic,
            createLogic: logic == "exchange"
                ? (stuffDataFactory, ctx) => new FurnitureExchangeLogic(stuffDataFactory, ctx)
                : null
        );
        _room.AddBox<WiredActionPlaceFurni>(2, 0, 0, "wf_act_place_furni");

        (await SaveLampAsync([LAMP])).IsSaved.Should().BeTrue();

        var result = await SaveLampAsync([picked]);

        // Only an own plain furni on a hotel that copies more than borrowed furni is allowed.
        var allowed = logic == "default_floor" && !buildersClubOnly;

        result.IsSaved.Should().Be(allowed);
        result.ErrorKey.Should().Be(allowed ? null : WiredSaveErrors.FURNI_CAN_NOT_BE_CLONED);
        ((FurnitureWiredLogic)_room.FloorItem(2).Logic)
            .GetStuffIds()
            .Should()
            .Equal(allowed ? picked : LAMP);
    }

    private Task<WiredSaveResult> SaveLampAsync(int[] stuffIds) =>
        _room.SaveWithResultAsync<UpdateActionMessage>(
            2,
            intParams: [0, 0, 0, 0, 1, 0, 0, 0, 0, 0],
            stuffIds: stuffIds,
            definitionSpecifics: [0]
        );

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
