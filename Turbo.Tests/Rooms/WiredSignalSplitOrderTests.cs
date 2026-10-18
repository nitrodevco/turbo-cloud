using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Send signal for each user" sends one signal per user in a random order, not in the order the
/// users were picked: a click on a furni, a selector picking every user, and a split send.
/// </summary>
public sealed class WiredSignalSplitOrderTests
{
    private const int CLICK_ME = 20;
    private const int ANTENNA = 21;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredSignalSplitOrderTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 7);
        _room.AddFloorItem(ANTENNA, 6, 7);

        for (var i = 0; i < 6; i++)
            _room.Enter(5 + i, 1 + i, 1);
    }

    [Fact]
    public async Task Split_signals_do_not_keep_one_order_from_send_to_send()
    {
        await BuildAsync();

        var heard = new SignalListener();
        using var registration = _room.Harness.EventListeners.Register([heard]);
        var orders = new HashSet<string>();

        for (var i = 0; i < 12; i++)
        {
            heard.Users.Clear();
            await ClickAsync();
            await TickAsync(1);

            heard.Users.Should().HaveCount(6);
            orders.Add(string.Join(",", heard.Users));
        }

        // Twelve sends of six users in one fixed order is a 1 in 720^11 chance.
        orders.Should().HaveCountGreaterThan(1);
    }

    private async Task BuildAsync()
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredSelectorEntitiesByType>(2, 0, 0, "wf_slc_users_bytype");
        _room.AddBox<WiredActionSendSignal>(3, 0, 0, "wf_act_send_signal");

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
            await _room.SaveAsync<UpdateSelectorMessage>(
                2,
                intParams: [1],
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [0, 1],
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

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

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }

    private sealed class SignalListener : IRoomEventListener
    {
        public List<int> Users { get; } = [];

        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            if (evt is WiredSignalEvent signal)
                Users.AddRange(signal.AvatarIds.Select(x => x.Value));

            return Task.CompletedTask;
        }
    }
}
