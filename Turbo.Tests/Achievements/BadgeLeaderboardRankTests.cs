using FluentAssertions;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

/// <summary>
/// Places on a badge leaderboard as Habbo numbers them (Top Badges, official client 2026-10-09):
/// a tie takes consecutive places, 107 "Richy" and 108 "Xine" both on 2431, in the board's
/// order (score, then player id). The player's own line is there even with no score, its rank
/// shown as "--" (-1).
/// </summary>
public sealed class BadgeLeaderboardRankTests : IDisposable
{
    private const int TOP = 2;
    private const int FIRST_TIED = 1;
    private const int SECOND_TIED = 3;
    private const int LAST = 4;
    private const int NO_BADGES = 5;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private int _nextBadgeId = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public BadgeLeaderboardRankTests()
    {
        for (var id = 1; id <= 5; id++)
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"player-{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        Badges(TOP, 3);
        Badges(FIRST_TIED, 2);
        Badges(SECOND_TIED, 2);
        Badges(LAST, 1);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_tie_takes_consecutive_places_in_player_id_order()
    {
        var page = await Board()
            .GetLeaderboardAsync(
                BadgeLeaderboardType.TotalBadges,
                rarity: 0,
                chunkIndex: 0,
                chunkSize: 10,
                forPlayerId: TOP,
                Ct
            );

        page.Entries.Select(x => (x.PlayerId.Value, x.Score, x.Rank))
            .Should()
            .Equal((TOP, 3, 1), (FIRST_TIED, 2, 2), (SECOND_TIED, 2, 3), (LAST, 1, 4));
    }

    [Fact]
    public async Task A_chunk_past_the_entries_held_goes_on_counting_places()
    {
        var page = await Board(heldEntries: 2)
            .GetLeaderboardAsync(
                BadgeLeaderboardType.TotalBadges,
                rarity: 0,
                chunkIndex: 1,
                chunkSize: 2,
                forPlayerId: TOP,
                Ct
            );

        page.Entries.Select(x => (x.PlayerId.Value, x.Rank))
            .Should()
            .Equal((SECOND_TIED, 3), (LAST, 4));
    }

    [Fact]
    public async Task The_own_line_outside_the_entries_held_has_its_place_on_the_board()
    {
        var page = await Board(heldEntries: 2)
            .GetLeaderboardAsync(
                BadgeLeaderboardType.TotalBadges,
                rarity: 0,
                chunkIndex: 0,
                chunkSize: 2,
                forPlayerId: SECOND_TIED,
                Ct
            );

        page.OwnEntry.Should().NotBeNull();
        (page.OwnEntry!.Rank, page.OwnEntry.Score).Should().Be((3, 2));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2)]
    public async Task A_player_with_no_score_still_gets_their_line_with_no_rank(int heldEntries)
    {
        var page = await Board(heldEntries)
            .GetLeaderboardAsync(
                BadgeLeaderboardType.TotalBadges,
                rarity: 0,
                chunkIndex: 0,
                chunkSize: 10,
                forPlayerId: NO_BADGES,
                Ct
            );

        page.OwnEntry.Should().NotBeNull();
        page.OwnEntry!.Name.Should().Be($"player-{NO_BADGES}");
        (page.OwnEntry.Rank, page.OwnEntry.Score).Should().Be((BadgeRanks.NONE, 0));
    }

    private IBadgeLeaderboardGrain Board(int heldEntries = 500)
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Badges.BadgeLeaderboardGrain",
            _fakes,
            _db
        );
        RoomHarness.SetField(
            grain,
            "_badgeConfig",
            new BadgeConfig { LeaderboardHeldEntries = heldEntries }
        );
        return (IBadgeLeaderboardGrain)grain;
    }

    private void Badges(int playerId, int count)
    {
        for (var i = 0; i < count; i++)
            _db.Insert(
                new PlayerBadgeEntity
                {
                    Id = _nextBadgeId,
                    PlayerEntityId = playerId,
                    BadgeCode = $"B{_nextBadgeId++}",
                    PlayerEntity = null!,
                }
            );
    }
}
