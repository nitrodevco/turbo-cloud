using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player walks into a room wearing their favourite group: Habbo's Users message on entry
/// already names it (official client capture, 2026-10-13: <c>groupId 402778</c>,
/// <c>groupName "[HIA] Agent ID V"</c>). Turbo loaded the group only after the avatar had been
/// sent, so everybody - the player included - saw no group badge until it was changed in the room.
/// </summary>
public sealed class FavouriteGroupOnEntryTests
{
    private const int PLAYER = 42;
    private const int GROUP = 901;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IEnumerable<IComposer> Broadcast() =>
        _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .SelectMany(call => ((RoomOutboundSnapshot)call.Args[0]!).Composers);

    [Fact]
    public async Task The_avatar_goes_out_wearing_its_favourite_group_with_the_badge_ahead_of_it()
    {
        var fakes = _room.Harness.Fakes;

        fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        fakes.Handlers["GetFavouriteGuildIdAsync"] = _ =>
            Task.FromResult<GuildId?>(GuildId.Parse(GROUP));
        fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult<GuildSummarySnapshot?>(
                new GuildSummarySnapshot
                {
                    GuildId = GuildId.Parse(GROUP),
                    Name = "grp e2e",
                    BadgeCode = "b01014",
                    RoomId = 1,
                    OwnerId = PLAYER,
                    PrimaryColorId = 1,
                    SecondaryColorId = 1,
                    PrimaryColor = "ffffff",
                    SecondaryColor = "ffffff",
                    Type = GuildType.Regular,
                    HasForum = false,
                    RightsLevel = GuildRightsLevel.Admins,
                }
            );
        fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call => new RoomPlayerAvatar
        {
            ObjectId = (RoomObjectId)call.Args[0]!,
            PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
        };

        await _room
            .Harness.Module<RoomAvatarModule>()
            .CreateAvatarFromPlayerAsync(
                ActionContext.CreateForPlayer((PlayerId)PLAYER, (RoomId)1),
                new PlayerSummarySnapshot
                {
                    PlayerId = (PlayerId)PLAYER,
                    Name = "member",
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

        var sent = Broadcast().ToList();
        var users = sent.OfType<UsersMessageComposer>().First();
        var avatar = users.Avatars.OfType<RoomPlayerAvatarSnapshot>().Single();

        avatar.GroupId.Should().Be(GROUP);
        avatar.GroupName.Should().Be("grp e2e");
        sent.FindIndex(x => x is HabboGroupBadgesMessageComposer)
            .Should()
            .BeInRange(0, sent.IndexOf(users) - 1);
    }
}
