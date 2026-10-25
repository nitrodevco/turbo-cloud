using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The selectors as the W4 lab measured them on an official room (official client capture
/// 2026-10-24, WIN63-202610201032); each test is named by its case id. Three furni stand at
/// 3,5 / 4,5 / 5,5; two users, Ann and Bob, at 1,1 and 2,1.
/// </summary>
public sealed class WiredSelectorLabAnswersTests
{
    private const int A = 30;
    private const int B = 31;
    private const int C = 32;

    private readonly WiredRoom _room = new(8, 8);
    private int _nextBox = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredSelectorLabAnswersTests()
    {
        foreach (var id in new[] { A, B, C })
            _room.AddFloorItem(id, id - 27, 5);

        _room.Enter(5, 1, 1, "Ann");
        _room.Enter(6, 2, 1, "Bob");
    }

    // selector.19.R7: picking ONE selector box of a parked pile runs every selector on that tile,
    // furni and user selectors alike, as a union.
    [Fact]
    public async Task Selector19R7_PickingOneBoxRunsTheWholePile()
    {
        var picked = await AddPicksAsync(0, 0, [C]);
        await AddPicksAsync(0, 0, [B]);
        await AddUsersByTypeAsync(0, 0);

        var set = await RemoteAsync(picked);

        set.SelectedFurniIds.Should().Equal(B, C);
        set.SelectedAvatarIds.Select(x => x.Value).Should().BeEquivalentTo([5, 6]);
    }

    // selector.2/3/8/9/11/12/13/14.R4e: every user selector lists in ONE shared order per room
    // state (official client capture 2026-10-25, W4 lab wlab9: ten user selectors, 3 states,
    // 50/50). Habbo's order itself follows no rule, so the test pins only that they agree.
    // Ann, Bob and Cid enter out of name order onto a 3x2 floor beside the middle furni.
    [Fact]
    public async Task SelectorR4e_EveryUserSelectorListsInTheOneSharedOrder()
    {
        _room.Harness.State.AvatarsByObjectId.Remove(5);
        _room.Harness.State.AvatarsByObjectId.Remove(6);
        _room.AddFloorItem(40, 3, 3, width: 3, length: 2);
        _room.Enter(7, 4, 3, "Bob", playerId: 106);
        _room.Enter(8, 3, 4, "Cid", playerId: 107);
        _room.Enter(9, 5, 4, "Ann", playerId: 105);

        IWiredSelector[] boxes =
        [
            await AddSelectorAsync<WiredSelectorEntitiesByType>(
                "wf_slc_users_bytype",
                intParams: [1]
            ),
            await AddSelectorAsync<WiredSelectorEntitiesByName>(
                "wf_slc_users_byname",
                stringParam: "Ann\tCid\tBob"
            ),
            await AddSelectorAsync<WiredSelectorEntitiesInArea>(
                "wf_slc_users_area",
                intParams: [0, 0, 8, 8]
            ),
            await AddSelectorAsync<WiredSelectorEntitiesByAction>(
                "wf_slc_users_byaction",
                intParams: [(int)WiredAvatarActionType.Stand]
            ),
            await AddSelectorAsync<WiredSelectorEntitiesInNeighborhood>(
                "wf_slc_users_neighborhood",
                intParams: [0, 0, 0, 33554431],
                stuffIds: [B]
            ),
            await AddSelectorAsync<WiredSelectorEntitiesWithHanditem>(
                "wf_slc_users_handitem",
                intParams: [0]
            ),
            await AddSelectorAsync<WiredSelectorEntitiesOnItem>(
                "wf_slc_users_onfurni",
                stuffIds: [40]
            ),
        ];
        var orders = new List<string>();

        foreach (var box in boxes)
            orders.Add($"{box.GetType().Name}: {string.Join(",", await RunChainAsync(box))}");

        var first = await RunChainAsync(boxes[0]);
        first.Order().Should().Equal(7, 8, 9);
        orders
            .Where(x => !x.EndsWith($": {string.Join(",", first)}"))
            .Should()
            .BeEmpty($"every user selector lists like {boxes[0].GetType().Name}");
    }

    // selector.19.R7: with an inverted selector in the pile the union is the whole room.
    [Fact]
    public async Task Selector19R7_AnInvertedSelectorInThePileGivesTheWholeRoom()
    {
        var picked = await AddPicksAsync(0, 0, [C]);
        await AddPicksAsync(0, 0, [C], invert: true);

        var set = await RemoteAsync(picked);

        set.SelectedFurniIds.Should().Contain([A, B, C]);
        set.SelectedFurniIds.Should().Contain(picked, "the selector itself is room furni too");
    }

    // selector.19.R5: two boxes of one pile are one stack, so "1 random stack" still runs both.
    [Fact]
    public async Task Selector19R5_TwoBoxesOfOnePileAreOneStack()
    {
        var first = await AddPicksAsync(0, 0, [C]);
        var second = await AddPicksAsync(0, 0, [A]);

        var set = await RemoteAsync([first, second], randomStacks: 1);

        set.SelectedFurniIds.Should().Equal(A, C);
    }

    // selector.1.R4 / selector.19.R4: the result comes in one room order, not in pick order and
    // not one selector after the other.
    [Fact]
    public async Task Selector1R4_TheResultIsInRoomOrderNotPickOrder()
    {
        var picked = await AddPicksAsync(0, 0, [C, A]);
        await AddPicksAsync(0, 0, [B]);

        var set = await RemoteAsync(picked);

        set.SelectedFurniIds.Should().Equal(A, B, C);
    }

    // selector.1.E11: Furni Picks has no furni source slot; a save that sends one anyway is
    // refused with invalid_furni_source_count (official client capture 2026-10-25T02-16-06, 5/5).
    [Fact]
    public async Task Selector1E11_ASourceForASlotThePicksDoNotHaveIsRefused()
    {
        _room.AddBox<WiredSelectorSelectedItems>(9, 0, 0, "wf_slc_furni_picks");

        var refused = await _room.SaveWithResultAsync<UpdateSelectorMessage>(
            9,
            stuffIds: [A],
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ],
            definitionSpecifics: [false, false]
        );

        refused.IsSaved.Should().BeFalse();
        refused.ErrorKey.Should().Be("wiredfurni.error.invalid_furni_source_count");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                9,
                stuffIds: [A],
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();
    }

    // selector.14.R5: hand item 0 (the editor's "none") picks the users holding nothing.
    [Fact]
    public async Task Selector14R5_HandItemZeroPicksTheEmptyHanded()
    {
        _room.Avatars[6].SetHandItem(4);
        var box = await AddSelectorAsync<WiredSelectorEntitiesWithHanditem>(
            "wf_slc_users_handitem",
            intParams: [0]
        );

        var set = await box.SelectAsync(Context(), Ct);

        set.SelectedAvatarIds.Select(x => x.Value).Should().Equal(5);
    }

    // selector.11.R7: case ignored, no trimming, a tab-separated list.
    [Theory]
    [InlineData("ann", new[] { 5 })]
    [InlineData("Ann ", new int[0])]
    [InlineData("nobody\tBob", new[] { 6 })]
    public async Task Selector11R7_NamesMatchAsTyped(string names, int[] expected)
    {
        var box = await AddSelectorAsync<WiredSelectorEntitiesByName>(
            "wf_slc_users_byname",
            stringParam: names
        );

        var set = await box.SelectAsync(Context(), Ct);

        set.SelectedAvatarIds.Select(x => x.Value).Should().Equal(expected);
    }

    // selector.4.R5: mode 2 ("Furni at same height") picks the picked furni itself.
    [Fact]
    public async Task Selector4R5_SameHeightPicksThePickedFurniItself()
    {
        _room.AddFloorItem(40, 6, 6);
        var top = _room.AddFloorItem(41, 6, 6);
        top.SetPositionZ(Altitude.FromInt(100));
        var box = await AddSelectorAsync<WiredSelectorItemsOnItem>(
            "wf_slc_furni_onfurni",
            intParams: [2],
            stuffIds: [40]
        );

        var set = await box.SelectAsync(Context(), Ct);

        set.SelectedFurniIds.Should().Equal(40);
    }

    // selector.16.R5: Furni With Altitude reads the absolute z, in 1/100, with 0 less / 1 equal /
    // 2 greater (held as it was).
    [Theory]
    [InlineData(100, 1, new[] { 41 })]
    [InlineData(100, 0, new[] { A, B, C, 40 })]
    [InlineData(0, 2, new[] { 41 })]
    public async Task Selector16R5_AltitudeIsTheAbsoluteZ(
        int altitude,
        int comparison,
        int[] expected
    )
    {
        _room.AddFloorItem(40, 6, 6);
        _room.AddFloorItem(41, 6, 6).SetPositionZ(Altitude.FromInt(100));
        var box = await AddSelectorAsync<WiredSelectorItemsWithAltitude>(
            "wf_slc_furni_altitude",
            intParams: [altitude, comparison]
        );

        var set = await box.SelectAsync(Context(), Ct);

        set.SelectedFurniIds.Where(x => x is >= A and <= 41).Order().Should().Equal(expected);
    }

    // selector.9.R5: a wave is picked 1.1 s after it, not 5.3 s after.
    [Theory]
    [InlineData(1_100, new[] { 5 })]
    [InlineData(5_300, new int[0])]
    public async Task Selector9R5_AWaveIsPickedWhileItLasts(int afterMs, int[] expected)
    {
        var box = await AddSelectorAsync<WiredSelectorEntitiesByAction>(
            "wf_slc_users_byaction",
            intParams: [(int)WiredAvatarActionType.Wave]
        );

        await _room
            .Harness.Module<RoomAvatarModule>()
            .SetAvatarExpressionAsync(5, AvatarExpressionType.Wave, Ct);
        _room.Clock.AdvanceMs(afterMs);

        var set = await box.SelectAsync(Context(), Ct);

        set.SelectedAvatarIds.Select(x => x.Value).Should().Equal(expected);
    }

    // action.7.L5: Show Message cuts its filled-in text at exactly 1000 characters.
    [Fact]
    public async Task Action7L5_ShowMessageIsCutAt1000Characters()
    {
        var wired = _room.Harness.Module<RoomWiredSystem>();
        var trigger = _nextBox++;
        var show = _nextBox++;

        _room.AddBox<WiredTriggerHabboSaysKeyword>(trigger, 0, 0, "wf_trg_says_something");
        _room.AddBox<WiredActionShowMessage>(show, 0, 0, "wf_act_show_message");
        (await _room.SaveAsync<UpdateTriggerMessage>(trigger, intParams: [0, 2, 0]))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                show,
                intParams: [1, 34, -1],
                stringParam: "$(trigger_text)",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        long now = 10_000;
        await wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await wired.ProcessWiredAsync(now += 1_000, dormant: false, Ct);
        await wired.OnRoomEventAsync(
            new PlayerChatEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1),
                PlayerId = (PlayerId)105,
                ObjectId = 5,
                ChatType = RoomChatType.Chat,
                StyleId = 0,
                TrackingId = 0,
                Gesture = default,
                Text = new string('x', 1_200),
            },
            Ct
        );
        await wired.ProcessWiredAsync(now += 1_000, dormant: false, Ct);
        await wired.ProcessWiredAsync(now + 1_000, dormant: false, Ct);

        _room
            .Harness.Fakes.SentToRoom<ChatMessageComposer>()
            .Select(x => x.Text.Length)
            .Should()
            .Equal(1_000);
    }

    private async Task<int> AddPicksAsync(int x, int y, int[] picks, bool invert = false)
    {
        var id = _nextBox++;

        _room.AddBox<WiredSelectorSelectedItems>(id, x, y, "wf_slc_furni_picks");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                stuffIds: picks,
                definitionSpecifics: [false, invert]
            )
        )
            .Should()
            .BeTrue();

        return id;
    }

    private async Task AddUsersByTypeAsync(int x, int y)
    {
        var id = _nextBox++;

        _room.AddBox<WiredSelectorEntitiesByType>(id, x, y, "wf_slc_users_bytype");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                intParams: [1],
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();
    }

    private async Task<T> AddSelectorAsync<T>(
        string logicName,
        int[]? intParams = null,
        int[]? stuffIds = null,
        string stringParam = ""
    )
        where T : FurnitureWiredSelectorLogic
    {
        var id = _nextBox++;
        var box = _room.AddBox<T>(id, 7, 7, logicName);

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                intParams: intParams,
                stuffIds: stuffIds,
                furniSources: stuffIds is null
                    ? null
                    :
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                stringParam: stringParam,
                definitionSpecifics: [false, false]
            )
        ).Should().BeTrue();

        return box;
    }

    private Task<IWiredSelectionSet> RemoteAsync(int picked, int randomStacks = 0) =>
        RemoteAsync([picked], randomStacks);

    private async Task<IWiredSelectionSet> RemoteAsync(int[] picked, int randomStacks = 0)
    {
        var id = _nextBox++;
        var remote = _room.AddBox<WiredSelectorRemoteSelection>(id, 7, 0, "wf_slc_remote");

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                intParams: [0, randomStacks],
                stuffIds: picked,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [false, false]
            )
        ).Should().BeTrue();

        // The pile is a stack of its own, loaded as the room loads it.
        var wired = _room.Harness.Module<RoomWiredSystem>();

        await wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0), _room.Map.ToIdx(7, 0)],
            },
            Ct
        );
        await wired.ProcessWiredAsync(10_000, dormant: false, Ct);

        return await remote.SelectAsync(Context(), Ct);
    }

    private async Task<int[]> RunChainAsync(IWiredSelector selector)
    {
        var pool = new WiredSelectionSet();

        await WiredSelectorChain.RunAsync(
            [selector],
            Context(),
            pool,
            _room.Harness.State.ItemsById.Values.ToList(),
            _room.Harness.State.AvatarsByObjectId.Values.ToList(),
            null,
            Ct
        );

        return [.. pool.SelectedAvatarIds.Select(x => x.Value)];
    }

    private WiredProcessingContext Context() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
}
