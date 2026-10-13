using FluentAssertions;
using Turbo.Database.Entities.Guilds;
using Turbo.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// A player who leaves (or is kicked from, or loses) the group whose badge they wear: their
/// memberships no longer hold it, so the next room they enter draws no badge. The room they are
/// standing in is told at once, the way a deselected favourite is (FavoriteMembershipUpdate with
/// group -1, official client capture 2026-10-13), instead of drawing a group they are not in
/// until they walk out.
/// </summary>
public sealed class FavouriteGroupLeftTests
{
    private const int PLAYER = 102;
    private const int GROUP = 901;
    private const int ROOM = 7;

    private readonly InMemoryDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IPlayerGuildGrain> MemberWearingTheGroupAsync()
    {
        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
        {
            dbCtx.GuildMembers.Add(
                new GuildMemberEntity
                {
                    GuildEntityId = GROUP,
                    PlayerEntityId = PLAYER,
                    Rank = GuildMemberRank.Member,
                    IsFavourite = true,
                }
            );
            await dbCtx.SaveChangesAsync(Ct);
        }

        _fakes.Handlers["GetActiveRoomAsync"] = _ =>
            Task.FromResult(new RoomPointerSnapshot { RoomId = ROOM, ActiveSinceUtc = DateTime.UtcNow });

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

    private IEnumerable<FakeCall> RoomTold() => _fakes.Log.Of("SetPlayerFavouriteGuildAsync");

    [Fact]
    public async Task Leaving_the_worn_group_takes_the_badge_off_in_the_room()
    {
        var grain = await MemberWearingTheGroupAsync();

        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
        {
            dbCtx.GuildMembers.RemoveRange(dbCtx.GuildMembers);
            await dbCtx.SaveChangesAsync(Ct);
        }

        await grain.OnMembershipsChangedAsync(Ct);

        var told = RoomTold().Should().ContainSingle().Subject;
        told.Args[1].Should().Be(-1);
        told.Args[2].Should().Be(GuildMembershipStatus.None);
        (await grain.GetFavouriteGuildIdAsync(Ct)).Should().BeNull();
    }

    [Fact]
    public async Task A_change_that_keeps_the_worn_group_tells_the_room_nothing()
    {
        var grain = await MemberWearingTheGroupAsync();

        await grain.OnMembershipsChangedAsync(Ct);

        RoomTold().Should().BeEmpty();
    }
}
