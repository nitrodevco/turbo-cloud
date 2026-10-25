using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// wlab12 answers of 2026-10-25 (official client capture, WIN63-202610201032, own room, full
/// recording wlab12-full-20261025-074418): a bot placed onto a furni walks on it and one picked up
/// from it walks off (trigger.1.R8 / trigger.2.R8, 3/3 + 3/3); two Click Furni triggers with the
/// same pick run their stack once per click and only the bottom one flashes (trigger.18.L4, 4/4);
/// Move As Group moving a stack keeps it stacked (action.57.B9, 2026-10-24T23-18-27).
/// </summary>
public sealed class WiredLab12LeftoverAnswersTests
{
    private const int CLICK_ME = 20;
    private const int TOP = 21;
    private const int BASE = 22;
    private const int OWNER_AVATAR = 5;
    private const int BOT = 31;

    private readonly WiredRoom _room = new(10, 10);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_bot_placed_onto_a_furni_walks_on_it_and_off_it_when_picked_up_trigger_1_R8()
    {
        var heard = new List<RoomEvent>();
        var block = _room.AddFloorItem(BASE, 4, 4);

        _room.Harness.EventListeners.Register([new Recorder(heard)]);
        _room.Harness.Room.UseAvatarProvider(
            new RoomAvatarProvider(Options.Create(new BotConfig()))
        );
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutBotAsync)] = call =>
            Task.FromResult<BotSnapshot?>(Bot((int)call.Args[0]!));
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnBotAsync)] = _ =>
            Task.FromResult(true);
        _room.Enter(OWNER_AVATAR, 1, 1, playerId: 1);
        var owner = ActionContext.CreateForPlayer((PlayerId)1, (RoomId)1);

        (await _room.Harness.Room.PlaceBotAsync(owner, BOT, 4, 4, Ct)).Should().BeTrue();
        _room.Harness.Module<RoomBotModule>().TryGetBot(BOT, out var bot).Should().BeTrue();
        (await _room.Harness.Room.PickupBotAsync(owner, BOT, Ct)).Should().BeTrue();

        heard
            .Where(e => e is AvatarWalkOnFurniEvent or AvatarWalkOffFurniEvent)
            .Select(e =>
                e switch
                {
                    AvatarWalkOnFurniEvent on => $"on {on.FurniId.Value} by {on.ObjectId.Value}",
                    AvatarWalkOffFurniEvent off =>
                        $"off {off.FurniId.Value} by {off.ObjectId.Value}",
                    _ => "",
                }
            )
            .Should()
            .Equal(
                $"on {block.ObjectId.Value} by {bot!.ObjectId.Value}",
                $"off {block.ObjectId.Value} by {bot.ObjectId.Value}"
            );
    }

    [Fact]
    public async Task Two_click_triggers_on_the_same_furni_run_their_stack_once_trigger_18_L4()
    {
        _room.AddFloorItem(CLICK_ME, 9, 9);
        _room.AddFloorItem(BASE, 4, 4);
        var bottom = _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        var toggle = _room.AddBox<WiredActionToggleItemState>(2, 0, 0, "wf_act_toggle_state");
        var second = _room.AddBox<WiredTriggerClickFurni>(3, 0, 0, "wf_trg_click_furni");

        foreach (var trigger in new[] { 1, 3 })
            (
                await _room.SaveAsync<UpdateTriggerMessage>(
                    trigger,
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
                intParams: [0],
                stuffIds: [BASE],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StartAsync();

        await ClickAsync();

        _room.FloorItem(BASE).Logic.GetState().Should().Be(1, "the stack ran once");
        toggle.GetState().Should().Be(1);
        bottom.GetState().Should().Be(1, "the bottom trigger flashed");
        second.GetState().Should().Be(0, "the second trigger never flashes");

        await ClickAsync();

        _room.FloorItem(BASE).Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task Move_as_group_keeps_a_picked_stack_stacked_action_57_B9()
    {
        _room.AddFloorItem(CLICK_ME, 9, 9);
        var baseItem = _room.AddFloorItem(BASE, 3, 3);
        var top = _room.AddFloorItem(TOP, 5, 5);
        _room.Map.MoveFloorItem(top, _room.Map.ToIdx(3, 3));
        top.Z.Should().Be(Altitude.FromInt(100));
        _room.Enter(OWNER_AVATAR, 6, 7);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMoveAsGroup>(2, 0, 0, "wf_act_move_furni_as_group");
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
        // The top's id is the lower one, so a pick listed by id names it first.
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [1, 0, -1],
                stuffIds: [TOP, BASE],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.TriggeredItem],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync();

        await ClickAsync();

        (baseItem.X, baseItem.Y, baseItem.Z).Should().Be((6, 6, Altitude.Zero));
        (top.X, top.Y, top.Z).Should().Be((6, 6, Altitude.FromInt(100)));
    }

    private static BotSnapshot Bot(int id) =>
        new()
        {
            Id = id,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = null,
            Name = "Frank",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            X = 0,
            Y = 0,
            Z = Altitude.Zero,
            Rotation = Rotation.South,
            FreeRoam = false,
            ChatText = string.Empty,
            AutoChat = false,
            ChatDelaySeconds = 0,
            MixSentences = false,
            DanceType = AvatarDanceType.None,
        };

    private async Task StartAsync()
    {
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

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(2);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }

    private sealed class Recorder(List<RoomEvent> heard) : IRoomEventListener
    {
        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            heard.Add(evt);

            return Task.CompletedTask;
        }
    }
}
