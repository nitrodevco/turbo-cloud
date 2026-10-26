using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Plugin policies over avatar effects (<see cref="IAvatarEffectPolicy"/>), for a hotel command
/// that turns effects off: on a player's own avatar (what it wears) or in what a player is shown
/// on everyone (each viewer's own packet). With none, the room sends what the avatar wears to
/// everyone in one packet, as before.
/// </summary>
public sealed class AvatarEffectPolicyTests
{
    private const int WEARER_AVATAR = 1;
    private const int VIEWER_AVATAR = 2;
    private const int WEARER = 100 + WEARER_AVATAR;
    private const int VIEWER = 100 + VIEWER_AVATAR;
    private const int LATECOMER = 300;
    private const int EFFECT = 116;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(8, 8);
    private readonly EffectsOff _policy = new();

    public AvatarEffectPolicyTests()
    {
        var fakes = _room.Harness.Fakes;

        fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call => new RoomPlayerAvatar
        {
            ObjectId = (RoomObjectId)call.Args[0]!,
            PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
        };
    }

    private void Seat()
    {
        _room.Enter(WEARER_AVATAR, 2, 2);
        _room.Enter(VIEWER_AVATAR, 4, 4);
    }

    [Fact]
    public async Task With_no_policy_the_effect_is_worn_and_sent_to_everyone_in_one_packet()
    {
        Seat();

        (await WearAsync()).Should().BeTrue();

        Wearer().EffectId.Should().Be(EFFECT);
        var publish = _room.Harness.Fakes.RoomPublishes().Should().ContainSingle().Subject;
        publish.ExcludedPlayerIds.Should().BeNullOrEmpty();
        _room
            .Harness.Fakes.SentToRoom<AvatarEffectMessageComposer>()
            .Select(x => (x.ObjectId.Value, x.EffectId))
            .Should()
            .Equal((WEARER_AVATAR, EFFECT));
    }

    [Fact]
    public async Task A_player_with_effects_off_wears_none_and_nothing_is_sent()
    {
        Seat();
        Register();
        _policy.WearsNone.Add(WEARER);

        (await WearAsync()).Should().BeFalse();

        Wearer().EffectId.Should().Be(0);
        _room.Harness.Fakes.SentToRoom<AvatarEffectMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task A_viewer_with_effects_hidden_is_sent_the_avatar_bare_while_it_wears_the_effect()
    {
        Seat();
        Register();
        _policy.SeesNone.Add(VIEWER);

        (await WearAsync()).Should().BeTrue();

        Wearer().EffectId.Should().Be(EFFECT, "the room (wired, its clean-up) reads what it wears");
        _room
            .Harness.Fakes.RoomPublishes()
            .Should()
            .ContainSingle()
            .Which.ExcludedPlayerIds.Should()
            .Equal((PlayerId)VIEWER);
        _room
            .Harness.Fakes.SentToRoom<AvatarEffectMessageComposer>()
            .Single()
            .EffectId.Should()
            .Be(EFFECT);
        _room
            .Harness.Fakes.SentToPlayer<AvatarEffectMessageComposer>(VIEWER)
            .Select(x => (x.ObjectId.Value, x.EffectId))
            .Should()
            .Equal((WEARER_AVATAR, 0));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_player_entering_with_effects_hidden_is_not_sent_the_effects_worn_there(
        bool hidden
    )
    {
        // Both come in as players do, so the room numbers their avatars.
        (await EnterAsync(WEARER))
            .Should()
            .NotBeEmpty();
        var wearer = Wearer().ObjectId;
        await _room.Harness.Module<RoomAvatarModule>().SetAvatarEffectAsync(wearer, EFFECT, Ct);
        Register();

        if (hidden)
            _policy.SeesNone.Add(LATECOMER);

        var tail = await EnterAsync(LATECOMER);

        tail.Should().Contain(x => x is UsersMessageComposer, "the player entered");
        tail.OfType<AvatarEffectMessageComposer>()
            .Select(x => (x.ObjectId.Value, x.EffectId))
            .Should()
            .Equal(hidden ? [] : [(wearer.Value, EFFECT)]);
    }

    private void Register() => _room.Harness.Policies.Register("effects-off", _policy);

    private Turbo.Primitives.Rooms.Object.Avatars.IRoomPlayer Wearer() =>
        _room.Harness.Module<RoomAvatarModule>().Players.Single(x => x.PlayerId == WEARER);

    private Task<bool> WearAsync() =>
        _room
            .Harness.Module<RoomAvatarModule>()
            .SetAvatarEffectAsync((RoomObjectId)WEARER_AVATAR, EFFECT, Ct);

    private async Task<IReadOnlyList<Turbo.Primitives.Networking.IComposer>> EnterAsync(
        int playerId
    ) =>
        await _room.Harness.Room.CreateAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer((PlayerId)playerId, (RoomId)1),
            new PlayerSummarySnapshot
            {
                PlayerId = (PlayerId)playerId,
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
            new RoomEntrySnapshot { Method = RoomEntryMethodType.Default, TeleportId = 0 },
            Ct
        );

    public sealed class EffectsOff : IAvatarEffectPolicy
    {
        public HashSet<long> WearsNone { get; } = [];
        public HashSet<long> SeesNone { get; } = [];

        public int Wear(AvatarEffectRequest request, int proposed) =>
            request.PlayerId is { } player && WearsNone.Contains(player.Value) ? 0 : proposed;

        public int Show(AvatarEffectView view, int shown) =>
            SeesNone.Contains(view.ViewerId.Value) ? 0 : shown;
    }
}
