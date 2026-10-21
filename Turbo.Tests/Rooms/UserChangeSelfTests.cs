using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player in a room whose look or score changes. Habbo tells the player a UserChange with room
/// index -1 (their own session, me menu icon and landing view avatar: SessionDataManager.as:443,
/// MeMenuIconLoader.as:119, AvatarImageWidget.as:80, build 1745) and the room the same change by
/// room index (official client captures: 9 of 9 own changes, figure 2 and achievement score 7).
/// Turbo sent only the room's.
/// </summary>
public sealed class UserChangeSelfTests
{
    private const int PLAYER = 5;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_player_is_told_their_own_change_as_index_minus_one_and_the_room_by_index()
    {
        _room.Enter(PLAYER, 1, 1);

        (
            await _room
                .Harness.Module<RoomAvatarModule>()
                .UpdateAvatarWithPlayerAsync(Summary("hd-180-14"), Ct)
        )
            .Should()
            .BeTrue();

        var own = _room
            .Harness.Fakes.Sent<UserChangeMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle()
            .Subject;
        own.ObjectId.Value.Should().Be(-1);
        own.Figure.Should().Be("hd-180-14");
        own.AchievementScore.Should().Be(456);

        _room
            .Harness.Fakes.SentToRoom<UserChangeMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(own with { ObjectId = PLAYER });
    }

    private static PlayerSummarySnapshot Summary(string figure) =>
        new()
        {
            PlayerId = 100 + PLAYER,
            Name = "player",
            Motto = "",
            Figure = figure,
            Gender = AvatarGenderType.Male,
            AchievementScore = 456,
            BadgesRank = 0,
            IsOnline = true,
            CreatedAt = new DateTime(2025, 3, 4, 5, 6, 0, DateTimeKind.Utc),
            LastUpdated = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc),
            RespectPoints = 0,
            RespectsLeft = 0,
            PetRespectsLeft = 0,
            RespectReplenishesLeft = 0,
        };
}
