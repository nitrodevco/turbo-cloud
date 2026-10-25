using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Large Block (<c>bc_block_1*1</c>, height 1, five states, customparams -0.25): its asset's
/// logic is <c>furniture_multiheight</c> and each state draws it a quarter lower. Reported as
/// "lowering the block keeps its original height": the block looked lower and everything still
/// stood on it at height 1, since the server knew it only as plain furniture.
/// </summary>
public sealed class MultiHeightBlockTests
{
    private const int BLOCK = 40;

    private readonly WiredRoom _room = new();
    private long _now = 10_000;

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 75)]
    [InlineData(2, 50)]
    [InlineData(4, 0)]
    public async Task Each_state_lowers_the_block_and_its_tile_a_quarter(int state, int height)
    {
        var block = AddBlock();

        await block.Logic.SetStateAsync(state);

        block.Logic.Should().BeOfType<FurnitureMultiHeightLogic>();
        block.GetStackHeight().ToInt().Should().Be(height);
        _room.Map.GetTileHeight(_room.Map.ToIdx(2, 2)).ToInt().Should().Be(height);
    }

    [Fact]
    public async Task The_room_is_sent_the_blocks_new_height()
    {
        var block = AddBlock();

        await block.Logic.SetStateAsync(2);

        _room
            .Harness.Fakes.SentToRoom<ObjectUpdateMessageComposer>()
            .Should()
            .Contain(x => x.FloorItem.ObjectId == BLOCK && x.FloorItem.StackHeight.ToInt() == 50);
    }

    // W2 action.0.X2: Toggle Furni State on a height block a user stands on. The user follows
    // the block's new top on the next user tick, 2.65 -> 2.4 -> 2.15 -> 1.9 -> 1.65 -> 2.65
    // (official client capture 2026-10-24T08-07-58, 5/5).
    [Fact]
    public async Task A_user_on_a_block_toggled_by_wired_follows_its_new_top()
    {
        var ct = TestContext.Current.CancellationToken;
        var block = AddBlock();
        var user = _room.Enter(5, 2, 2);
        var toggle = _room.AddBox<WiredActionToggleItemState>(7, 0, 0, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                7,
                intParams: [0],
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        _room.Map.UpdateHeightForAvatar(user);
        user.Z.ToInt().Should().Be(100);

        foreach (var height in new[] { 75, 50, 25, 0, 100 })
        {
            await toggle.ExecuteAsync(
                new WiredExecutionContext(_room.Harness.Room) { CancellationToken = ct },
                ct
            );

            user.NeedsInvoke.Should().BeTrue("the next user tick puts the user on the new top");

            await _room.Map.InvokeAvatarAsync(user, ct);

            user.Z.ToInt().Should().Be(height);
        }
    }

    // W2 action.0.B4: block K at its lowest state (0 high) with block K2 put on it at the same
    // altitude; Toggle Furni State picking both leaves K as it is and toggles K2, and toggles K
    // again once K2 is picked up (official client capture 2026-10-24, WIN63-202610201032: K 0/5,
    // K2 2/2, K after the pickup 2/2). Ours toggled K under K2.
    [Fact]
    public async Task Action0B4_ABlockWithAnotherOnTopIsNotToggledAndTheTopOneIs()
    {
        const int TOP = 41;
        var ct = TestContext.Current.CancellationToken;
        var block = AddBlock();

        await block.Logic.SetStateAsync(4);

        var top = AddBlock(TOP);
        var toggle = _room.AddBox<WiredActionToggleItemState>(7, 0, 0, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                7,
                intParams: [0],
                stuffIds: [BLOCK, TOP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        async Task<List<RoomObjectId>> ToggleAsync()
        {
            var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = ct };

            await toggle.ExecuteAsync(ctx, ct);

            return [.. ctx.FloorItemStateUpdates.Select(x => x.Item1)];
        }

        (await ToggleAsync()).Should().Equal((RoomObjectId)TOP);
        block.Logic.GetState().Should().Be(4);
        top.Logic.GetState().Should().Be(1);

        _room.Map.RemoveItem(top);
        _room.Harness.State.ItemsById.Remove(TOP);

        (await ToggleAsync()).Should().Equal((RoomObjectId)BLOCK);
        block.Logic.GetState().Should().Be(0);
    }

    // W2 trigger.4.B4 / action.0.B4c: a hand use of block K at its lowest state with block K2 on
    // it got no reply and K kept its state (2/2); K2 toggled (official client capture 2026-10-24
    // 22-26-27). Furni Is Used picking K did not fire for those uses, and fired for the uses of
    // K uncovered (2026-10-25T01-03, 0/2 and 2/2). Ours stepped K under K2, then fired the
    // trigger for the use it refused.
    [Fact]
    public async Task A_block_with_another_on_top_is_not_changed_by_hand_trigger_4_B4()
    {
        const int TOP = 41;
        const int LAMP = 42;
        var ct = TestContext.Current.CancellationToken;
        var block = AddBlock();
        var lamp = _room.AddFloorItem(LAMP, 6, 6);

        await FurniIsUsedTogglesAsync(BLOCK, LAMP);
        await block.Logic.SetStateAsync(4);

        var top = AddBlock(TOP);

        for (var use = 0; use < 2; use++)
        {
            await UseAndRunWiredAsync(BLOCK);

            lamp.Logic.GetState().Should().Be(0, "Furni Is Used does not fire for a refused use");
        }

        block.Logic.GetState().Should().Be(4);

        await UseAndRunWiredAsync(TOP);

        top.Logic.GetState().Should().Be(1);

        _room.Map.RemoveItem(top);
        _room.Harness.State.ItemsById.Remove(TOP);
        await UseAndRunWiredAsync(BLOCK);

        lamp.Logic.GetState().Should().Be(1, "the uncovered block's use fires it");

        async Task UseAndRunWiredAsync(int itemId)
        {
            await _room.Harness.Room.UseItemByIdAsync(ActionContext.CreateForSystem(1), itemId, ct);
            await RunWiredAsync();
        }
    }

    private async Task FurniIsUsedTogglesAsync(int used, int toggled)
    {
        const int TRIGGER = 50;
        const int EFFECT = 51;
        var ct = TestContext.Current.CancellationToken;

        _room.AddBox<WiredTriggerHabboChangesItemState>(TRIGGER, 0, 0, "wf_trg_state_changed");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [used],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [toggled],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(0, 0)],
                },
                ct
            );
        await RunWiredAsync();
    }

    private async Task RunWiredAsync()
    {
        var wired = _room.Harness.Module<RoomWiredSystem>();

        for (var i = 0; i < 10; i++)
        {
            _now += 50;
            await wired.ProcessWiredAsync(
                _now,
                dormant: false,
                TestContext.Current.CancellationToken
            );
        }
    }

    private IRoomFloorItem AddBlock(int id = BLOCK)
    {
        var item = _room.AddFloorItem(id, 2, 2, "bc_block_1*1");

        item.GetType()
            .GetProperty(nameof(item.Definition))!
            .SetValue(
                item,
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

        return item;
    }
}
