using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Wired as the wlab6 lab measured it on an official room (official client capture 2026-10-24,
/// WIN63-202610201032, explore/wlab6-full-20261024-194548); each test names its wired matrix case.
/// </summary>
public sealed class WiredLab6AnswersTests
{
    private const int CLICK_ME = 20;
    private const int FLOOR = 21;
    private const int LOG = 2;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // E8 (every family; measured on action.7 / action.49): a save while a delayed run is pending
    // is accepted at once, the pending run keeps the text and delay it had when the trigger fired
    // ("E8B" with delay 0 saved mid-delay: "E8A" still came at the old +3 s, 4/4), and the next
    // firing runs the new save.
    [Fact]
    public async Task E8_ASaveDuringAPendingDelayedRunAppliesFromTheNextFiringOnly()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionWriteToLogs>(LOG, 0, 0, "wf_act_log");
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
        (await SaveLogAsync("E8A", delayPulses: 6)).IsSaved.Should().BeTrue();
        await StartAsync(0, 0);

        await ClickAsync();
        await TickAsync(100);
        await TickAsync(1_000);

        (await SaveLogAsync("E8B", delayPulses: 0)).IsSaved.Should().BeTrue();
        await TickAsync(1_000);

        Logs().Should().BeEmpty("the pending run still waits its old 3 s");

        await TickAsync(1_100);

        Logs().Should().Equal("E8A");

        await ClickAsync();
        await TickAsync(100);

        Logs().Should().Equal("E8A", "E8B");
    }

    // E2 (all 159 box types, wired matrix *.E2): 21 picks are refused with the server error by
    // every box, a box without a pick list too, before anything the box itself checks; nothing
    // of the save is kept.
    [Fact]
    public async Task E2_TwentyOnePicksAreAServerErrorForEveryBox()
    {
        var offenders = new List<string>();

        foreach (var classname in OfficialBoxes())
        {
            var room = new WiredRoom(8, 8);

            for (var i = 0; i < 21; i++)
                room.AddFloorItem(30 + i, i % 8, 1 + (i / 8));

            var box = AddBox(room, classname);
            var result = await SaveAsync(room, box.WiredType, [.. Enumerable.Range(30, 21)]);

            await box.EnsureWiredLoadedAsync(Ct);

            if (result.ServerErrorCode != 1 || box.GetSnapshot().StuffIds.Count > 0)
                offenders.Add(
                    $"{classname}: saved {result.IsSaved}, error {result.ErrorKey ?? result.ServerErrorCode?.ToString()}"
                );
        }

        offenders.Should().BeEmpty();
    }

    // selector.8.R4: Users On Furni lists the users by the furni's tiles, (5,10), (5,9), (6,9),
    // (6,10) for a 2x2 at 5,9 (here 2,3): Frank on 5,9, frank on 5,10, Hutch. on 6,9 gave
    // "frank,Frank,Hutch."; Frank 5,9, frank 6,9, Hutch. 6,10 gave "Frank,frank,Hutch." (10/10).
    [Theory]
    [InlineData(3, 3, 2, 3, 2, 4, new[] { 12, 11, 10 })]
    [InlineData(3, 4, 2, 3, 3, 3, new[] { 11, 12, 10 })]
    public async Task Selector8R4_UsersOnFurniFollowTheFurnisTiles(
        int hutchX,
        int hutchY,
        int bigFrankX,
        int bigFrankY,
        int smallFrankX,
        int smallFrankY,
        int[] expected
    )
    {
        _room.AddFloorItem(FLOOR, 2, 3, width: 2, length: 2);
        _room.Enter(10, hutchX, hutchY, "Hutch.");
        _room.Enter(11, bigFrankX, bigFrankY, "Frank");
        _room.Enter(12, smallFrankX, smallFrankY, "frank");
        var box = _room.AddBox<WiredSelectorEntitiesOnItem>(3, 7, 7, "wf_slc_users_onfurni");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                3,
                stuffIds: [FLOOR],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [false, false]
            )
        ).Should().BeTrue();

        (await RunChainAsync([box], new WiredSelectionSet())).Should().Equal(expected);
    }

    // selector.10.R4: Users From Signal keeps the order the sender's selection had (4/4).
    [Fact]
    public async Task Selector10R4_UsersFromSignalKeepTheSendersOrder()
    {
        _room.Enter(10, 1, 1, "Hutch.");
        _room.Enter(11, 2, 1, "Frank");
        _room.Enter(12, 3, 1, "frank");
        var box = _room.AddBox<WiredSelectorEntitiesFromSignal>(3, 7, 7, "wf_slc_users_signal");
        (await _room.SaveAsync<UpdateSelectorMessage>(3, definitionSpecifics: [false, false]))
            .Should()
            .BeTrue();

        var signal = new WiredSelectionSet();
        signal.SelectedAvatarIds.UnionWith([12, 10, 11]);

        (await RunChainAsync([box], signal)).Should().Equal(12, 10, 11);
    }

    // addon.11.R2: two user filters on one stack, amount 2 and amount 1: one user per firing, the
    // smaller limit wins (3/3).
    [Fact]
    public async Task Addon11R2_OfTwoUserFiltersTheSmallerAmountWins()
    {
        foreach (var (id, amount) in new[] { (4, 2), (5, 1) })
        {
            _room.AddBox<WiredAddonUserSelectorFilter>(id, 7, 7, "wf_xtra_filter_users");
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    id,
                    intParams: [amount, 0, (int)WiredVariableTargetType.User]
                )
            )
                .Should()
                .BeTrue();
        }

        var ctx = Context(new WiredSelectionSet());
        ctx.SelectorPool.SelectedAvatarIds.UnionWith([10, 11, 12]);

        foreach (var id in new[] { 4, 5 })
            await ((IWiredAddon)_room.FloorItem(id).Logic).MutatePolicyAsync(ctx, Ct);

        ctx.SelectorPool.SelectedAvatarIds.Should().ContainSingle();
    }

    private Task<WiredSaveResult> SaveLogAsync(string text, int delayPulses) =>
        _room.SaveWithResultAsync<UpdateActionMessage>(
            LOG,
            intParams: [(int)WiredLogLevelType.Info],
            stringParam: text,
            definitionSpecifics: [delayPulses]
        );

    private List<string> Logs() => [.. Wired.GetRoomLogs().Select(x => x.Message)];

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

    private async Task StartAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );

        await TickAsync(100);
    }

    private async Task TickAsync(int ms)
    {
        _now += ms;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
    }

    private async Task<int[]> RunChainAsync(IWiredSelector[] selectors, WiredSelectionSet signal)
    {
        var pool = new WiredSelectionSet();

        await WiredSelectorChain.RunAsync(
            selectors,
            Context(signal),
            pool,
            _room.Harness.State.ItemsById.Values.ToList(),
            _room.Harness.State.AvatarsByObjectId.Values.ToList(),
            null,
            Ct
        );

        return [.. pool.SelectedAvatarIds.Select(x => x.Value)];
    }

    private WiredProcessingContext Context(WiredSelectionSet signal) =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
            Signal = signal,
        };

    private static Task<WiredSaveResult> SaveAsync(WiredRoom room, WiredType type, int[] picks) =>
        type switch
        {
            WiredType.Trigger => room.SaveWithResultAsync<UpdateTriggerMessage>(
                BOX,
                stuffIds: picks
            ),
            WiredType.Condition => room.SaveWithResultAsync<UpdateConditionMessage>(
                BOX,
                stuffIds: picks
            ),
            WiredType.Addon => room.SaveWithResultAsync<UpdateAddonMessage>(BOX, stuffIds: picks),
            WiredType.Selector => room.SaveWithResultAsync<UpdateSelectorMessage>(
                BOX,
                stuffIds: picks
            ),
            WiredType.Variable => room.SaveWithResultAsync<UpdateVariableMessage>(
                BOX,
                stuffIds: picks
            ),
            _ => room.SaveWithResultAsync<UpdateActionMessage>(BOX, stuffIds: picks),
        };

    private const int BOX = 2;

    private static FurnitureWiredLogic AddBox(WiredRoom room, string classname)
    {
        var type = typeof(FurnitureWiredLogic)
            .Assembly.GetTypes()
            .Single(type =>
                type.GetCustomAttribute<RoomObjectLogicAttribute>(false)?.Key == classname
            );

        return (FurnitureWiredLogic)
            typeof(WiredRoom)
                .GetMethod(nameof(WiredRoom.AddBox))!
                .MakeGenericMethod(type)
                .Invoke(room, [BOX, 0, 0, classname])!;
    }

    /// <summary>Every box type Habbo was recorded opening (the editor contract fixture).</summary>
    private static List<string> OfficialBoxes()
    {
        using var stream = typeof(WiredLab6AnswersTests).Assembly.GetManifestResourceStream(
            "Rooms.Fixtures.official-wired-editor-opens.json"
        )!;
        using var json = JsonDocument.Parse(stream);

        return
        [
            .. json
                .RootElement.EnumerateArray()
                .Select(x => x.GetProperty("classname").GetString()!),
        ];
    }
}
