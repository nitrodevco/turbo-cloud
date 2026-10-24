using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A Projectile add-on as it is placed, never saved, turns the furni the stack moves to the way
/// it flies and turns the shooter, the clicker by default, to face that way too: Habbo moved a
/// block +2,0 with it, rotation 2 and curve 50, and the clicker, standing three tiles south facing
/// 4, came in the same WiredMovements as userDirections body 2 head 2 (official client capture
/// 2026-10-24T07-42-18, WIN63-202610201032, 1/1).
/// </summary>
public sealed class WiredProjectileShooterTests
{
    private const int CLICKER = 5;
    private const int CLICK_ME = 20;
    private const int BLOCK = 21;

    private readonly WiredRoom _room = new(10, 12);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // addon.21.R1
    [Fact]
    public async Task Addon21R1_ANeverSavedProjectileTurnsTheFurniAndTheShooterTheWayItFlies()
    {
        var clicker = _room.Enter(CLICKER, 7, 11, rotation: Rotation.South);
        _room.AddFloorItem(CLICK_ME, 8, 11);
        _room.AddFloorItem(BLOCK, 5, 8);
        _room.AddBox<WiredTriggerClickFurni>(1, 9, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionRelativeMove>(2, 9, 0, "wf_act_rel_mov");
        _room.AddBox<WiredAddonProjectile>(3, 9, 0, "wf_xtra_rotate_to_dir");

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
                intParams: [2, 0],
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(9, 0)],
            },
            Ct
        );
        await TickAsync(1);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(4);

        var block = _room.FloorItem(BLOCK);

        (block.X, block.Y, block.Rotation).Should().Be((7, 8, Rotation.East));
        (clicker.Rotation, clicker.HeadRotation).Should().Be((Rotation.East, Rotation.East));

        var sent = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().ToList();

        sent.SelectMany(x => x.FloorItems).Single().CurveStrength.Should().Be(50);
        sent.SelectMany(x => x.UserDirections)
            .Select(x => (x.ObjectId.Value, x.BodyRotation, x.HeadRotation))
            .Should()
            .Equal((CLICKER, Rotation.East, Rotation.East));
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
