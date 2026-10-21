using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player in a room who takes a new name: the room is told with UserNameChanged (web id, room
/// index, new name), which each client applies by room index and the player's own session takes
/// as its name (AS3 1318 RoomUsersHandler.onUserNameChange :278, SessionDataManager
/// .onUserNameChange :517). Any other update of the player is no rename.
/// </summary>
public sealed class UserNameChangedTests
{
    private const int PLAYER = 5;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("newname", true)]
    [InlineData("oldname", false)]
    public async Task A_new_name_is_told_to_the_room_by_room_index(string name, bool told)
    {
        var avatar = _room.Enter(PLAYER, 1, 1, "oldname");

        (
            await _room
                .Harness.Module<RoomAvatarModule>()
                .UpdateAvatarWithPlayerAsync(Summary(name), Ct)
        )
            .Should()
            .BeTrue();

        var sent = _room.Harness.Fakes.SentToRoom<UserNameChangedMessageComposer>().ToList();

        if (told)
            sent.Should()
                .ContainSingle()
                .Which.Should()
                .Be(
                    new UserNameChangedMessageComposer
                    {
                        WebId = 100 + PLAYER,
                        Id = PLAYER,
                        Name = "newname",
                    }
                );
        else
            sent.Should().BeEmpty();
    }

    private static PlayerSummarySnapshot Summary(string name) =>
        new()
        {
            PlayerId = 100 + PLAYER,
            Name = name,
            Motto = "",
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            AchievementScore = 0,
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
