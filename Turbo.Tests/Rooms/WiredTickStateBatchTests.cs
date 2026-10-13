using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo sends the floor states wired changes as one ObjectsDataUpdate every 500 ms (recorded on
/// hhus, WIN63-202610121709, a public wired room: one message every 500 ms carrying about 65 furni,
/// the tiles the effects changed and the boxes that flashed), not a message per effect and per flash.
/// </summary>
public sealed class WiredTickStateBatchTests
{
    private const int CLICK_ME = 20;
    private const int PAD_A = 21;
    private const int PAD_B = 22;
    private const int TRIGGER = 1;
    private const int EFFECT_A = 2;
    private const int EFFECT_B = 3;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Every_state_a_tick_changes_goes_out_in_one_ObjectsDataUpdate()
    {
        await BuildAsync();

        await ClickAsync();

        // Ten 50 ms wired ticks: the trigger fires on one, its effects run on the next, and the
        // states go out once, when the 500 ms window ends.
        List<IComposer> sent = [];
        for (var i = 0; i < 10; i++)
            sent.AddRange(await TickAsync());

        var batches = sent.OfType<ObjectsDataUpdateMessageComposer>().ToList();
        batches.Should().ContainSingle();
        batches[0]
            .StuffDatas.Select(x => x.Item1)
            .Should()
            .BeEquivalentTo(
                new[] { PAD_A, PAD_B, TRIGGER, EFFECT_A, EFFECT_B }.Select(id =>
                    _room.FloorItem(id).ObjectId
                )
            );
        sent.OfType<ObjectDataUpdateMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task A_state_a_player_sets_before_the_update_goes_out_is_the_one_sent()
    {
        await BuildAsync();
        await ClickAsync();
        List<IComposer> sent = [];
        sent.AddRange(await TickAsync());
        sent.AddRange(await TickAsync());
        var pad = _room.FloorItem(PAD_A);
        pad.Logic.GetState().Should().Be(1);

        await pad.Logic.SetStateAsync(0);
        for (var i = 0; i < 8; i++)
            sent.AddRange(await TickAsync());

        sent.OfType<ObjectsDataUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.StuffDatas.Single(x => x.Item1 == pad.ObjectId)
            .Item2.Should()
            .Be(pad.Logic.StuffData.GetSnapshot());
    }

    private async Task BuildAsync()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(PAD_A, 3, 3);
        _room.AddFloorItem(PAD_B, 4, 3);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionToggleItemState>(EFFECT_A, 0, 0, "wf_act_toggle_state");
        _room.AddBox<WiredActionToggleItemState>(EFFECT_B, 0, 0, "wf_act_toggle_state");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        await SaveToggleAsync(EFFECT_A, PAD_A);
        await SaveToggleAsync(EFFECT_B, PAD_B);
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync();
    }

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

    private async Task SaveToggleAsync(int effect, int pad) => (
            await _room.SaveAsync<UpdateActionMessage>(
                effect,
                intParams: [0],
                stuffIds: [pad],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

    private async Task<List<IComposer>> TickAsync()
    {
        var before = _room.Harness.Fakes.Log.Calls.Count;
        _now += 50;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

        return _room
            .Harness.Fakes.Log.Calls.Skip(before)
            .SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .ToList();
    }
}
