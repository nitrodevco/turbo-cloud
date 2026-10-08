using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player who comes from another room through a teleport arrives standing in this room's half:
/// that is stepping onto it, so "user walks on furni" fires for it. The Wired Faculty tutorial
/// "Adding a Sandtrap effect to floor hatch teleports" (02/10/2026) unfreezes the player with such
/// a stack in the room they arrive in. Here the stack toggles a lamp.
/// </summary>
public sealed class WiredTeleportArrivalWalkOnTests
{
    private const int TELEPORT = 20;
    private const int LAMP = 21;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Arriving_in_a_teleport_from_another_room_is_walking_onto_it()
    {
        _room.AddFloorItem(
            TELEPORT,
            3,
            3,
            "teleport",
            createLogic: (factory, ctx) => new FurnitureTeleportLogic(factory, ctx)
        );
        var lamp = _room.AddFloorItem(LAMP, 6, 6);

        _room.AddBox<WiredTriggerWalkOnFurni>(1, 0, 0, "wf_trg_walks_on_furni");
        _room.AddBox<WiredActionToggleItemState>(2, 0, 0, "wf_act_toggle_state");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [TELEPORT],
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
                stuffIds: [LAMP],
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
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);

        _room.Harness.Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(
                Turbo.Primitives.Players.Snapshots.Permissions.ResolvedPermissionsSnapshot.EMPTY
            );
        _room.Harness.Fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call =>
        {
            var avatar = new RoomPlayerAvatar
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
            };

            return avatar;
        };

        var player = await _room
            .Harness.Module<RoomAvatarModule>()
            .CreateAvatarFromPlayerAsync(
                ActionContext.CreateForPlayer((PlayerId)42, (RoomId)1),
                new PlayerSummarySnapshot
                {
                    PlayerId = (PlayerId)42,
                    Name = "traveller",
                    Motto = string.Empty,
                    Figure = string.Empty,
                    Gender = default,
                    AchievementScore = 0,
                    BadgesRank = 0,
                    IsOnline = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    RespectPoints = 0,
                    RespectsLeft = 0,
                    PetRespectsLeft = 0,
                    RespectReplenishesLeft = 0,
                },
                new RoomEntrySnapshot
                {
                    Method = RoomEntryMethodType.Teleport,
                    TeleportId = TELEPORT,
                },
                Ct
            );
        await TickAsync(3);

        (player.X, player.Y).Should().Be((3, 3));
        lamp.Logic.GetState().Should().Be(1);
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
