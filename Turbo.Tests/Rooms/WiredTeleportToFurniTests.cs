using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Teleport To Furni" with a signal's users, as a hotel builds it: a stack sends a signal with
/// every user a selector picked, a second stack receives it and teleports those users onto a
/// furni. The box is saved the way the client sends it, and run with a real execution context.
/// A teleport puts a user down without walking, so users already standing on the furni do not stop
/// the others; a closed tile and a furni nobody can stand on still do. Without "fast teleportation"
/// the user lands two seconds after the box fired (official client capture 2026-10-16).
/// </summary>
public sealed class WiredTeleportToFurniTests
{
    private const int BOX = 7;
    private const int TARGET = 30;

    private const long FIRED_AT = 10_000;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredTeleportToFurniTests()
    {
        // Three users standing apart; one furni, on a tile of its own, to teleport them to.
        _room.Enter(5, 1, 1);
        _room.Enter(6, 1, 3);
        _room.Enter(7, 3, 1);
    }

    [Fact]
    public async Task EveryUserTheSignalCarries_IsTeleportedOntoTheFurni()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers);

        var moved = await ExecuteAsync(box, signalUsers: [5, 6, 7], triggerer: 5);

        moved.Should().BeTrue();
        _room.Positions().Should().Equal("5@5,5", "6@5,5", "7@5,5");
    }

    [Fact]
    public async Task OnlyTheTriggererIsTeleported_WhenTheBoxUsesTheTriggeredUser()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.TriggeredUser);

        await ExecuteAsync(box, signalUsers: [5, 6, 7], triggerer: 5);

        _room.Positions().Should().Equal("5@5,5", "6@1,3", "7@3,1");
    }

    [Fact]
    public async Task ASignalWithNoUsers_TeleportsNobody()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers);

        var moved = await ExecuteAsync(box, signalUsers: [], triggerer: 5);

        moved.Should().BeFalse();
        _room.Positions().Should().Equal("5@1,1", "6@1,3", "7@3,1");
    }

    [Fact]
    public async Task TheSourceTheClientPicksIsKeptWhenTheBoxIsSaved()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers);

        box.GetPlayerSources()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Equal(WiredPlayerSourceType.SignalUsers);
        box.GetFurniSources()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Equal(WiredFurniSourceType.SelectedItems);
    }

    [Fact]
    public async Task ATeleportOntoAClosedTile_IsStillRefused()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers);
        _room.TileFlags()[_room.Map.ToIdx(5, 5)] |= RoomTileFlags.Closed;

        var moved = await ExecuteAsync(box, signalUsers: [5, 6, 7], triggerer: 5);

        moved.Should().BeFalse();
        _room.Positions().Should().Equal("5@1,1", "6@1,3", "7@3,1");
    }

    [Fact]
    public async Task ATeleportOntoAFurniNobodyCanStandOn_IsStillRefused()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers, targetCanWalk: false);

        var moved = await ExecuteAsync(box, signalUsers: [5, 6, 7], triggerer: 5);

        moved.Should().BeFalse();
        _room.Positions().Should().Equal("5@1,1", "6@1,3", "7@3,1");
    }

    [Fact]
    public async Task ASlideOntoAUser_StillStops_UnlessTheMovementPhysicsLetItThrough()
    {
        // 6 stands on the tile 5 is slid to.
        PutUserOn(6, 2, 1);

        var blocked = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };
        var through = new WiredExecutionContext(_room.Harness.Room)
        {
            Policy = new WiredPolicy { MovePhysics = WiredMovePhysicsFlags.MoveThroughUsers },
            CancellationToken = Ct,
        };
        var avatar = _room.Avatars[5];
        var tile = _room.Map.ToIdx(2, 1);

        (await blocked.ProcessUserMovementAsync(avatar, tile, SlideAvatarMoveType.Slide))
            .Should()
            .BeFalse("a slide does not go through a user by default");
        (await through.ProcessUserMovementAsync(avatar, tile, SlideAvatarMoveType.Slide))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ATeleportOntoAUser_IsNotBlockedByThem()
    {
        PutUserOn(6, 2, 1);
        var context = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        (
            await context.ProcessUserMovementAsync(
                _room.Avatars[5],
                _room.Map.ToIdx(2, 1),
                SlideAvatarMoveType.None
            )
        )
            .Should()
            .BeTrue();
        _room.Positions().Should().Equal("5@2,1", "6@2,1", "7@3,1");
    }

    // --- "Fast teleportation" ---

    // Official client capture 2026-10-16 (walk-on + Teleport To, 2 of 2 runs): AvatarEffect 4 with
    // the box flash, UserUpdate at the target +2.0 s, AvatarEffect 0 +3.5 s and no WiredMovements.
    // Ours sent a WiredMovements glide (500 ms) in the tick the box fired.
    [Fact]
    public async Task ATeleport_ShowsTheEffect_LandsTwoSecondsLater_AndSendsNoWiredMovement()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers);
        await Wired.ProcessWiredAsync(FIRED_AT, dormant: false, Ct);

        var context = await RunAsync(box, signalUsers: [5]);

        context.UserMoves.Should().BeEmpty();
        _room.Avatars[5].EffectId.Should().Be(4);
        _room.Positions().Should().Equal("5@1,1", "6@1,3", "7@3,1");

        await Wired.ProcessWiredAsync(FIRED_AT + 1_950, dormant: false, Ct);
        _room.Positions().Should().Equal("5@1,1", "6@1,3", "7@3,1");

        await Wired.ProcessWiredAsync(FIRED_AT + 2_000, dormant: false, Ct);
        _room.Positions().Should().Equal("5@5,5", "6@1,3", "7@3,1");
        _room.Avatars[5].EffectId.Should().Be(4);

        await Wired.ProcessWiredAsync(FIRED_AT + 3_500, dormant: false, Ct);
        _room.Avatars[5].EffectId.Should().Be(0);
    }

    // Official client capture 2026-10-16 (walk-on + Teleport To with "fast teleportation", 2 of 2
    // runs): AvatarEffect 235 with the box flash, the walk ends on the tile being stepped onto,
    // UserUpdate at the target +1.0 s, AvatarEffect 0 +1.5 s, no WiredMovements. Ours sent a
    // WiredMovements with no animation time in the tick the box fired.
    [Fact]
    public async Task FastTeleportation_ShowsEffect235_StopsTheWalk_AndLandsOneSecondLater()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers, fast: true);
        var avatars = _room.Harness.Module<RoomAvatarModule>();
        await Wired.ProcessWiredAsync(FIRED_AT, dormant: false, Ct);
        (await avatars.WalkAvatarToAsync(_room.Avatars[5], 1, 6, Ct)).Should().BeTrue();
        _room.Avatars[5].IsWalking.Should().BeTrue();

        var context = await RunAsync(box, signalUsers: [5]);

        context.UserMoves.Should().BeEmpty();
        _room.Avatars[5].EffectId.Should().Be(235);
        _room.Avatars[5].IsWalking.Should().BeFalse();

        await Wired.ProcessWiredAsync(FIRED_AT + 950, dormant: false, Ct);
        _room.Positions()[0].Should().NotBe("5@5,5");

        await Wired.ProcessWiredAsync(FIRED_AT + 1_000, dormant: false, Ct);
        _room.Positions()[0].Should().Be("5@5,5");
        _room.Avatars[5].EffectId.Should().Be(235);

        await Wired.ProcessWiredAsync(FIRED_AT + 1_500, dormant: false, Ct);
        _room.Avatars[5].EffectId.Should().Be(0);
    }

    [Fact]
    public async Task FastTeleportation_DoesNotLeaveUsersWhoAreAlreadyOnTheFurni()
    {
        var box = await PlaceBoxAsync(WiredPlayerSourceType.SignalUsers, fast: true);
        PutUserOn(5, 5, 5);

        await ExecuteAsync(box, signalUsers: [5, 6, 7], triggerer: 5);

        // Whoever is there stays there, and the others arrive: the option is about the animation.
        _room.Positions().Should().Equal("5@5,5", "6@5,5", "7@5,5");
    }

    private async Task<WiredExecutionContext> RunAsync(WiredActionTeleportTo box, int[] signalUsers)
    {
        var context = new WiredExecutionContext(_room.Harness.Room)
        {
            Signal = new WiredSelectionSet([], signalUsers.Select(x => (RoomObjectId)x)),
            CancellationToken = Ct,
        };

        await box.ExecuteAsync(context, Ct);

        return context;
    }

    private void PutUserOn(int objectId, int x, int y)
    {
        var avatar = _room.Avatars[objectId];

        _room.Map.RemoveAvatarAtIdx(avatar, _room.Map.ToIdx(avatar.X, avatar.Y), false);
        avatar.SetPosition(x, y);
        _room.Map.AddAvatarAtIdx(avatar, _room.Map.ToIdx(x, y), false);
    }

    private async Task<WiredActionTeleportTo> PlaceBoxAsync(
        WiredPlayerSourceType playerSource,
        bool targetCanWalk = true,
        bool fast = false
    )
    {
        _room.AddFloorItem(TARGET, 5, 5, canWalk: targetCanWalk);

        var box = _room.AddBox<WiredActionTeleportTo>(BOX, 2, 2, "wf_act_teleport_to");

        // The client's save: the picked furni, the sources it chose and a zero delay.
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [fast ? 1 : 0],
                stuffIds: [TARGET],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [playerSource],
                ],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        return box;
    }

    private async Task<bool> ExecuteAsync(
        WiredActionTeleportTo box,
        int[] signalUsers,
        int triggerer
    )
    {
        var context = new WiredExecutionContext(_room.Harness.Room)
        {
            Signal = new WiredSelectionSet([], signalUsers.Select(x => (RoomObjectId)x)),
            Selected = new WiredSelectionSet([], [(RoomObjectId)triggerer]),
            CancellationToken = Ct,
        };

        var moved = await box.ExecuteAsync(context, Ct);

        // Past the landing of a teleport that is not fast.
        await Wired.ProcessWiredAsync(FIRED_AT, dormant: false, Ct);

        return moved;
    }
}
