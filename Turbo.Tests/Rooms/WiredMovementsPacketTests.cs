using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// W1 case "furni + carried users in ONE WiredMovements, furni first": the room hears of a
/// stack's movements once, in one WiredMovements, its entries furni first, then wall items, then
/// users, then user turns. Official client capture 2026-10-03 (carry users, a walk tile moved one
/// tile east with the user on it): one packet, the tile's move then the user's, same 500 ms, 1/1;
/// every official recording of a WiredMovements with more than one kind of entry (254 packets,
/// 9 recordings, WIN63-202610131318 to 202610201032) lists furni before users and users before
/// user turns, 254/254. Ours sent a carried user in a packet of its own, before the furni's, and
/// wrote users first in any packet.
/// </summary>
public sealed class WiredMovementsPacketTests
{
    private const int CLICK_ME = 20;
    private const int TILE = 21;
    private const int USER = 5;

    // The move box's "move west" arrow.
    private const int MOVE_WEST = 7;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // W1 addon.8: the 2026-10-03 capture's case, one move of a carried user's tile.
    [Fact]
    public async Task A_carried_user_moves_in_the_furnis_packet()
    {
        await BuildAsync(inOrder: false, MOVE_WEST);

        var packets = await ClickAsync();

        packets.Should().ContainSingle();
        Describe(packets[0].FloorItems).Should().Equal("21: 3,3 -> 2,3");
        Describe(packets[0].Users).Should().Equal("5: 3,3 -> 2,3");
    }

    // W1 X1 (inferred from the furni rule, 2026-10-22 capture: a furni moved by several effects
    // is told once, start to final): the user it carries is told the same way.
    [Fact]
    public async Task A_user_carried_by_two_moves_is_told_once_from_start_to_final()
    {
        await BuildAsync(inOrder: false, MOVE_WEST, MOVE_WEST);

        var packets = await ClickAsync();

        packets.Should().ContainSingle();
        Describe(packets[0].FloorItems).Should().Equal("21: 3,3 -> 1,3");
        Describe(packets[0].Users).Should().Equal("5: 3,3 -> 1,3");
    }

    // W1 X1 with Execute In Order (2026-10-22 capture: only the furni's last step is sent): the
    // carried user's last step goes with it.
    [Fact]
    public async Task With_execute_in_order_a_carried_user_is_told_its_last_step()
    {
        await BuildAsync(inOrder: true, MOVE_WEST, MOVE_WEST);

        var packets = await ClickAsync();

        packets.Should().ContainSingle();
        Describe(packets[0].FloorItems).Should().Equal("21: 2,3 -> 1,3");
        Describe(packets[0].Users).Should().Equal("5: 2,3 -> 1,3");
    }

    [Fact]
    public void A_packet_lists_furni_then_wall_items_then_users_then_user_turns()
    {
        var packet = PacketHarness.Encode(
            new WiredMovementsMessageComposer
            {
                Users =
                [
                    new()
                    {
                        ObjectId = new RoomObjectId(USER),
                        SourceX = 7,
                        SourceY = 7,
                        SourceZ = Altitude.Zero,
                        TargetX = 8,
                        TargetY = 7,
                        TargetZ = Altitude.Zero,
                        MoveType = SlideAvatarMoveType.Slide,
                        AnimationTime = 500,
                        BodyDirection = Rotation.East,
                        HeadDirection = Rotation.East,
                        JumpPower = 0,
                    },
                ],
                FloorItems =
                [
                    new()
                    {
                        ObjectId = new RoomObjectId(TILE),
                        SourceX = 7,
                        SourceY = 7,
                        SourceZ = Altitude.Zero,
                        TargetX = 8,
                        TargetY = 7,
                        TargetZ = Altitude.Zero,
                        Rotation = Rotation.North,
                        AnimationTime = 500,
                        OvershootDistance = null,
                        CurveStrength = null,
                    },
                ],
                WallItems =
                [
                    new()
                    {
                        ObjectId = new RoomObjectId(30),
                        IsDirectionRight = true,
                        SourceX = 1,
                        SourceY = 1,
                        SourceOffsetX = 2,
                        SourceOffsetY = 3,
                        TargetX = 1,
                        TargetY = 2,
                        TargetOffsetX = 2,
                        TargetOffsetY = 3,
                        AnimationTime = 500,
                    },
                ],
                UserDirections =
                [
                    new()
                    {
                        ObjectId = new RoomObjectId(USER),
                        BodyRotation = Rotation.South,
                        HeadRotation = Rotation.South,
                    },
                ],
            }
        );

        packet.PopInt().Should().Be(4);

        // The capture's bytes: type 1, the tile 7,7 -> 8,7, "0.0", "0.0", its id, 500 ms, rotation
        // 0, no overshoot, no curve; then type 0, the user the same way.
        packet.PopInt().Should().Be((int)WiredMovementType.FloorItem);
        SkipFloorMove(packet).Should().Be(TILE);
        packet.PopInt().Should().Be((int)WiredMovementType.WallItem);
        SkipWallMove(packet).Should().Be(30);
        packet.PopInt().Should().Be((int)WiredMovementType.User);
        SkipUserMove(packet).Should().Be(USER);
        packet.PopInt().Should().Be((int)WiredMovementType.UserDirection);
        packet.PopInt().Should().Be(USER);
        packet.PopInt();
        packet.PopInt();
        packet.Remaining.Should().Be(0);
    }

    private static int SkipFloorMove(ClientPacket packet)
    {
        for (var i = 0; i < 4; i++)
            packet.PopInt();

        packet.PopString();
        packet.PopString();

        var id = packet.PopInt();

        packet.PopInt();
        packet.PopInt();
        packet.PopBoolean().Should().BeFalse();
        packet.PopBoolean().Should().BeFalse();

        return id;
    }

    private static int SkipWallMove(ClientPacket packet)
    {
        var id = packet.PopInt();

        packet.PopBoolean();

        for (var i = 0; i < 9; i++)
            packet.PopInt();

        return id;
    }

    private static int SkipUserMove(ClientPacket packet)
    {
        for (var i = 0; i < 4; i++)
            packet.PopInt();

        packet.PopString();
        packet.PopString();

        var id = packet.PopInt();

        for (var i = 0; i < 4; i++)
            packet.PopInt();

        packet.PopBoolean().Should().BeFalse();

        return id;
    }

    private static string[] Describe(IEnumerable<WiredFloorItemMovementSnapshot> moves) =>
        [
            .. moves.Select(m =>
                $"{(int)m.ObjectId}: {m.SourceX},{m.SourceY} -> {m.TargetX},{m.TargetY}"
            ),
        ];

    private static string[] Describe(IEnumerable<WiredUserMovementSnapshot> moves) =>
        [
            .. moves.Select(m =>
                $"{(int)m.ObjectId}: {m.SourceX},{m.SourceY} -> {m.TargetX},{m.TargetY}"
            ),
        ];

    /// <summary>
    /// Click furni -> one move effect per entry of <paramref name="moves"/> on a walkable tile at
    /// 3,3 the user stands on, with the carry-users add-on (users on the furni, everyone).
    /// </summary>
    private async Task BuildAsync(bool inOrder, params int[] moves)
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(TILE, 3, 3);
        var user = _room.Enter(USER, 3, 3, "rider");
        await SettleAsync(user);

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
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

        _room.AddBox<WiredAddonCarryUsers>(2, 0, 0, "wf_xtra_mov_carry_users");
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                2,
                intParams: [(int)WiredCarryUserType.StandingOnFurni],
                playerSources:
                [
                    [WiredPlayerSourceType.AllRoomUsers],
                ]
            )
        ).Should().BeTrue();

        if (inOrder)
            _room.AddBox<WiredAddonExecuteInOrder>(3, 0, 0, "wf_xtra_exec_in_order");

        var box = 4;

        foreach (var move in moves)
        {
            _room.AddBox<WiredActionMoveRotateFurni>(box, 0, 0, "wf_act_move_rotate");
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    box,
                    intParams: [move, 0],
                    stuffIds: [TILE],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();

            box++;
        }

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

    private async Task SettleAsync(RoomPlayerAvatar user)
    {
        await _room.Map.InvokeAvatarAsync(user, Ct);

        _room.Clock.AdvanceMs(500);
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_room.Harness.NowMs(), Ct);
    }

    /// <summary>The WiredMovements one click sent.</summary>
    private async Task<List<WiredMovementsMessageComposer>> ClickAsync()
    {
        var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        return [.. _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Skip(before)];
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
