using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Plugin arrival policies (<see cref="IRoomArrivalPolicy"/>, docs/plugin-policies.md): asked as
/// the room makes the avatar of a player it has let in, they place the player on a tile of their
/// own instead of the door and send them walking, reading what the plugin's furni sent with them
/// (<see cref="RoomEntrySnapshot.Extension"/>). A tile the player cannot stand on means the door;
/// a player arriving inside a teleporter is the teleporter's.
/// </summary>
public sealed class RoomArrivalPolicyTests
{
    private const int PLAYER = 300;
    private const int LINKER = 40;
    private static readonly RoomId FROM_ROOM = new(12);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private readonly Portals _policy = new();

    public RoomArrivalPolicyTests()
    {
        var fakes = _room.Harness.Fakes;

        fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call => new RoomPlayerAvatar
        {
            ObjectId = (RoomObjectId)call.Args[0]!,
            PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
        };
        _room.Harness.Policies.Register("portals", _policy);
    }

    /// <summary>
    /// Also for a player a plugin's linked pair sent (<see cref="RoomEntrySnapshot.ThroughLinkedPair"/>):
    /// passing the door does not make them a teleporter's arrival.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_policy_reading_the_extension_places_the_player_off_the_door_and_walks_them(
        bool throughLinkedPair
    )
    {
        await EnterAsync(
            Portal("""{"x":4,"y":6}""") with
            {
                ThroughLinkedPair = throughLinkedPair,
            }
        );

        var arrived = Arrived();
        (arrived.X, arrived.Y).Should().Be((4, 6));
        arrived.Rotation.Should().Be(Rotation.East);
        arrived.IsWalking.Should().BeTrue();
        arrived.GoalTileId.Should().Be(_room.Map.ToIdx(7, 6));
        _policy
            .Asked.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                new RoomArrivalRequest(
                    (PlayerId)PLAYER,
                    _room.Harness.Room.RoomId,
                    RoomEntryMethodType.Default,
                    FROM_ROOM,
                    new RoomEntryExtension("portals", """{"x":4,"y":6}""")
                )
            );
    }

    [Theory]
    [InlineData(20, 3, "off the map")]
    [InlineData(5, 5, "under a furni nobody stands on")]
    public async Task A_start_tile_the_player_cannot_stand_on_falls_back_to_the_door(
        int x,
        int y,
        string why
    )
    {
        _room.AddFloorItem(1, 5, 5, canWalk: false);

        await EnterAsync(Portal($$"""{"x":{{x}},"y":{{y}}}"""));

        (Arrived().X, Arrived().Y).Should().Be((0, 0), why);
    }

    [Fact]
    public async Task A_player_arriving_inside_a_teleporter_is_the_teleporters_and_no_policy_is_asked()
    {
        _room.AddFloorItem(
            LINKER,
            6,
            2,
            "wf_room_linker",
            createLogic: (stuff, ctx) => new FurnitureRoomLinkerLogic(stuff, ctx)
        );

        await EnterAsync(
            new RoomEntrySnapshot
            {
                Method = RoomEntryMethodType.Teleport,
                TeleportId = LINKER,
                SourceRoomId = FROM_ROOM,
                Extension = new RoomEntryExtension("portals", """{"x":4,"y":6}"""),
            }
        );

        (Arrived().X, Arrived().Y).Should().Be((6, 2));
        _policy.Asked.Should().BeEmpty();
    }

    private static RoomEntrySnapshot Portal(string payload) =>
        new()
        {
            Method = RoomEntryMethodType.Default,
            TeleportId = 0,
            SourceRoomId = FROM_ROOM,
            Extension = new RoomEntryExtension("portals", payload),
        };

    private IRoomPlayer Arrived() =>
        _room.Harness.Module<RoomAvatarModule>().Players.Single(x => x.PlayerId == PLAYER);

    private Task EnterAsync(RoomEntrySnapshot entry) =>
        _room.Harness.Room.CreateAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer((PlayerId)PLAYER, _room.Harness.Room.RoomId),
            new PlayerSummarySnapshot
            {
                PlayerId = (PlayerId)PLAYER,
                Name = "player",
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
            entry,
            Ct
        );

    /// <summary>A portal plugin's policy: its players appear where the payload says, facing east, and walk three tiles on.</summary>
    public sealed class Portals : IRoomArrivalPolicy
    {
        public List<RoomArrivalRequest> Asked { get; } = [];

        public ValueTask<RoomArrivalDecision> DecideAsync(
            RoomArrivalRequest request,
            RoomArrivalDecision proposed,
            CancellationToken ct
        )
        {
            Asked.Add(request);

            if (
                request.Extension is not { Key: "portals" } extension
                || System.Text.Json.JsonSerializer.Deserialize<Spot>(extension.Payload, JSON)
                    is not { } spot
            )
                return ValueTask.FromResult(proposed);

            return ValueTask.FromResult(
                proposed.StartAt(spot.X, spot.Y, Rotation.East) with
                {
                    WalkTo = new RoomTilePoint(spot.X + 3, spot.Y),
                }
            );
        }

        private static readonly System.Text.Json.JsonSerializerOptions JSON = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private sealed record Spot(int X, int Y);
    }
}
