using FluentAssertions;
using Turbo.Database.Entities.Guilds;
using Turbo.Guilds;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// The group badge a player wears goes into the room with what they are to that group: Habbo's
/// Users and FavoriteMembershipUpdate carry a plain member as 3 and an admin as 2 (official client
/// capture 2026-10-13: members of groups 402778, 402781, 402813, 303029 and 368818 at 3, an admin
/// of 368818 at 2). Turbo sent 1 for everybody. The owner is not recorded; 1 is inferred.
/// </summary>
public sealed class WornGroupStatusTests
{
    private const int PLAYER = 102;
    private const int GROUP = 901;
    private const int ROOM = 7;

    private readonly InMemoryDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IPlayerGuildGrain> PlayerAsync(GuildMemberRank rank, bool wearing)
    {
        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
        {
            dbCtx.GuildMembers.Add(
                new GuildMemberEntity
                {
                    GuildEntityId = GROUP,
                    PlayerEntityId = PLAYER,
                    Rank = rank,
                    IsFavourite = wearing,
                }
            );
            await dbCtx.SaveChangesAsync(Ct);
        }

        _fakes.Handlers["GetActiveRoomAsync"] = _ =>
            Task.FromResult(
                new RoomPointerSnapshot { RoomId = ROOM, ActiveSinceUtc = DateTime.UtcNow }
            );
        _fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult<GuildSummarySnapshot?>(
                new GuildSummarySnapshot
                {
                    GuildId = GuildId.Parse(GROUP),
                    Name = "worn",
                    BadgeCode = "b01014",
                    RoomId = ROOM,
                    OwnerId = 1,
                    PrimaryColorId = 1,
                    SecondaryColorId = 1,
                    PrimaryColor = "ffffff",
                    SecondaryColor = "ffffff",
                    Type = GuildType.Regular,
                    HasForum = false,
                    RightsLevel = GuildRightsLevel.Admins,
                }
            );

        var grain = GrainHarness.Create(
            typeof(GuildModule).Assembly,
            "Turbo.Guilds.Grains.PlayerGuildGrain",
            _fakes,
            _db,
            PLAYER
        );
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);

        return (IPlayerGuildGrain)grain;
    }

    [Theory]
    [InlineData(GuildMemberRank.Member, 3)]
    [InlineData(GuildMemberRank.Admin, 2)]
    [InlineData(GuildMemberRank.Owner, 1)]
    public async Task Room_entry_reads_the_wearers_rank_as_Habbo_numbers_it(
        GuildMemberRank rank,
        int expected
    )
    {
        var grain = await PlayerAsync(rank, wearing: true);

        ((int)await grain.GetFavouriteGuildStatusAsync(Ct)).Should().Be(expected);
    }

    [Theory]
    [InlineData(GuildMemberRank.Member, 3)]
    [InlineData(GuildMemberRank.Admin, 2)]
    public async Task Putting_the_badge_on_tells_the_room_the_wearers_rank(
        GuildMemberRank rank,
        int expected
    )
    {
        var grain = await PlayerAsync(rank, wearing: false);

        (await grain.SetFavouriteGuildAsync(GuildId.Parse(GROUP), Ct)).Should().BeTrue();

        var told = _fakes.Log.Of("SetPlayerFavouriteGuildAsync").Should().ContainSingle().Subject;
        told.Args[1].Should().Be(GROUP);
        ((int)(WornGuildStatus)told.Args[2]!).Should().Be(expected);
    }

    [Fact]
    public async Task Wearing_no_badge_is_minus_one()
    {
        var grain = await PlayerAsync(GuildMemberRank.Admin, wearing: false);

        ((int)await grain.GetFavouriteGuildStatusAsync(Ct)).Should().Be(-1);
    }
}
