using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Games;
using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Games;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Games;

/// <summary>
/// The game centre as a player meets it: closed until a game is registered, then a games-left
/// count from the settings, a lobby that counts down once it has its players, the game's arena
/// opened and every player forwarded to it, the game counted, and the leaderboards read from the
/// reported scores.
/// </summary>
public sealed class GameCenterTests : IDisposable
{
    private const int ALICE = 1;
    private const int BOB = 2;
    private const int ARENA = 77;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(ManualTimeProvider.FixedStart);
    private readonly GameRegistry _registry = new();
    private readonly TestOptionsMonitor<GameCenterConfig> _config = new(new());
    private readonly GameScoreStore _scores;

    public GameCenterTests()
    {
        foreach (var (id, name) in new[] { (ALICE, "alice"), (BOB, "bob") })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                }
            );

        _scores = new GameScoreStore(
            _db,
            _config,
            _registry,
            _clock,
            NullLogger<GameScoreStore>.Instance
        );
        _fakes.Handlers["GetSummaryAsync"] = call => Task.FromResult(Summary((int)(long)call.Key!));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Closed_until_a_game_is_registered_then_open_with_the_days_free_games()
    {
        var directory = Directory();

        (await directory.GetDirectoryStatusAsync(ALICE, Ct))
            .Status.Should()
            .Be(GameCenter.DIRECTORY_UNAVAILABLE);

        using var _ = _registry.Register([new TestGame()], []);
        var status = await directory.GetDirectoryStatusAsync(ALICE, Ct);

        status.Status.Should().Be(GameCenter.DIRECTORY_AVAILABLE);
        status.FreeGamesLeft.Should().Be(3);
        status.GamesPlayed.Should().Be(0);
    }

    [Fact]
    public async Task A_full_lobby_counts_down_opens_the_arena_and_forwards_its_players()
    {
        var game = new TestGame();
        using var _ = _registry.Register([game], []);
        var directory = Directory();

        await directory.JoinAsync(ALICE, null, Ct);
        _fakes.SentToPlayer<Game2GameCreatedMessageComposer>(ALICE).Should().ContainSingle();
        _fakes.SentToPlayer<Game2StartCounterMessageMessageComposer>(ALICE).Should().BeEmpty();

        await directory.JoinAsync(BOB, null, Ct);
        _fakes
            .SentToPlayer<Game2UserJoinedGameMessageComposer>(ALICE)
            .Single()
            .Player.TeamId.Should()
            .Be(2);
        _fakes
            .SentToPlayer<Game2StartCounterMessageMessageComposer>(BOB)
            .Single()
            .CountDownLength.Should()
            .Be(10);

        await directory.LaunchDueAsync(Ct);
        game.Launches.Should().BeEmpty("the countdown is not over");

        _clock.AdvanceMs(10_000);
        await directory.LaunchDueAsync(Ct);

        game.Launches.Should().ContainSingle().Which.Players.Should().HaveCount(2);
        _fakes.SentToPlayer<Game2GameStartedMessageComposer>(BOB).Should().ContainSingle();
        _fakes
            .Log.Of("ForwardToRoomAsync")
            .Select(call => (long)call.Key!)
            .Should()
            .BeEquivalentTo(new long[] { ALICE, BOB });

        var account = await directory.GetAccountStatusAsync(ALICE, TestGame.TYPE, Ct);
        account.GamesPlayedTotal.Should().Be(1);
        account.FreeGamesLeft.Should().Be(2);
    }

    [Fact]
    public async Task No_free_games_left_refuses_the_join()
    {
        _config.Set(new GameCenterConfig { FreeGamesPerDay = 0 });
        using var _ = _registry.Register([new TestGame()], []);

        await Directory().JoinAsync(ALICE, null, Ct);

        _fakes
            .SentToPlayer<Game2JoiningGameFailedMessageMessageComposer>(ALICE)
            .Single()
            .Reason.Should()
            .Be(GameCenter.JOIN_FAILED_NO_FREE_GAMES);
    }

    [Fact]
    public async Task Reported_scores_rank_the_leaderboard_and_reach_the_reward_handlers()
    {
        var rewards = new TestRewards();
        using var _ = _registry.Register([new TestGame()], [rewards]);

        await _scores.ReportAsync(
            new GameResult
            {
                GameTypeId = TestGame.TYPE,
                GameId = 1,
                Players =
                [
                    new GamePlayerResult(ALICE, 40, 1, 2),
                    new GamePlayerResult(BOB, 90, 2, 1),
                ],
            },
            Ct
        );

        var board = await _scores.GetLeaderboardAsync(
            ALICE,
            new GameLeaderboardQuery
            {
                Kind = GameLeaderboardKind.Weekly,
                GameTypeId = TestGame.TYPE,
                StartRank = -1,
                Upward = false,
                WindowSize = 50,
            },
            Ct
        );

        board
            .Entries.Select(entry => (entry.Name, entry.Score, entry.Rank))
            .Should()
            .Equal(("bob", 90, 1), ("alice", 40, 2));
        board.TotalListSize.Should().Be(2);
        rewards.Results.Should().ContainSingle();
    }

    private GameDirectoryGrain Directory()
    {
        var grain = GrainHarness.Create<GameDirectoryGrain>(_fakes, _db);

        GrainHarness.Replace(grain, _registry);
        GrainHarness.Replace(grain, _scores);
        GrainHarness.Replace(
            grain,
            new DefaultGameAccessPolicy(_config, _fakes.Create<Orleans.IGrainFactory>(), _scores)
        );
        GrainHarness.Replace<Microsoft.Extensions.Options.IOptionsMonitor<GameCenterConfig>>(
            grain,
            _config
        );
        GrainHarness.Replace<TimeProvider>(grain, _clock);

        return grain;
    }

    private static PlayerSummarySnapshot Summary(int playerId) =>
        new()
        {
            PlayerId = playerId,
            Name = playerId == ALICE ? "alice" : "bob",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            AchievementScore = 0,
            BadgesRank = 0,
            IsOnline = true,
            CreatedAt = DateTime.UnixEpoch,
            LastUpdated = DateTime.UnixEpoch,
            RespectPoints = 0,
            RespectsLeft = 0,
            PetRespectsLeft = 0,
            RespectReplenishesLeft = 0,
        };

    public sealed class TestGame : IRoomGame
    {
        public const int TYPE = 100;

        public List<GameLaunch> Launches { get; } = [];

        public GameDefinition Definition { get; } =
            new()
            {
                GameTypeId = TYPE,
                Key = "test",
                LevelName = "Test",
                NumberOfTeams = 2,
                MinPlayers = 2,
                MaxPlayers = 4,
            };

        public Task<GameArena> OpenArenaAsync(GameLaunch launch, CancellationToken ct)
        {
            Launches.Add(launch);

            return Task.FromResult(new GameArena(new RoomId(ARENA)));
        }
    }

    public sealed class TestRewards : IGameRewardHandler
    {
        public List<GameResult> Results { get; } = [];

        public Task OnGameResultAsync(GameResult result, CancellationToken ct)
        {
            Results.Add(result);

            return Task.CompletedTask;
        }
    }
}
