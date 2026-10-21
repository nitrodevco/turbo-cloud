using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Database.Migrations;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.RewardTracks;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// Reward tracks are kept in the database, not configuration: Habbo's two tracks are put there
/// as its client was sent them (RewardTracks, 2026-10-18 official client capture), a configured
/// track is moved in, and a saved change is what players get next, without a restart.
/// </summary>
public sealed class RewardTrackStoreTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private RewardTrackConfig _config = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Habbos_introduction_and_habboween_tracks_are_seeded_whole_once()
    {
        await SeedAsync();
        await SeedAsync();
        var store = Store();

        await store.ReloadAsync(Ct);

        store.Tracks.Select(x => x.Id).Should().Equal("introduction", "habboween2026");
        var introduction = store.Tracks[0];
        introduction.Theme.Should().Be("blue");
        introduction.Premium!.TaskPointsBoost.Should().Be(1.2);
        (introduction.Premium.CostCredits, introduction.Premium.CostDiamonds)
            .Should()
            .Be((100, 100));
        introduction.Tasks.Should().HaveCount(30);
        introduction.Tasks.Sum(x => x.Levels.Count).Should().Be(61);
        introduction
            .Tasks[0]
            .Levels.Select(x => (x.RequiredCount, x.Points))
            .Should()
            .Equal((1, 10), (5, 20), (20, 30));
        introduction.Prizes.Should().HaveCount(49);
        introduction.Prizes.Count(x => x.Premium).Should().Be(18);
        introduction
            .Prizes.Should()
            .Contain(x => x.Id == "free_008" && x.ProductType == ProductDisplayType.Pet)
            .And.Contain(x => x.Id == "premium_011" && x.ProductType == ProductDisplayType.Bot)
            .And.Contain(x => x.Id == "free_031" && x.RewardTypeId == "JRH31");

        var habboween = store.Tracks[1];
        habboween.Theme.Should().Be("red");
        (habboween.Premium!.CostCredits, habboween.Premium.CostDiamonds).Should().Be((75, 75));
        habboween.Tasks.Should().HaveCount(17);
        habboween.Tasks.Sum(x => x.Levels.Count).Should().Be(31);
        habboween.Tasks.Sum(x => x.Levels.Sum(l => l.Points)).Should().Be(1800);
        habboween.Tasks.Count(x => x.ActionType == "CUSTOM_WIRED_ACTION").Should().Be(9);
        habboween.Prizes.Should().HaveCount(60);
        habboween.Prizes.Min(x => x.RequiredPoints).Should().Be(100);
        habboween
            .Prizes.Should()
            .Contain(x => x.Id == "free30" && x.RequiredPoints == 1650 && x.RewardTypeId == "DAD18")
            .And.Contain(x => x.Id == "premium30" && x.RewardTypeId == "DAD19");
        RewardTrackRules.Problem(store.Tracks).Should().BeNull();
    }

    [Fact]
    public async Task Every_action_type_of_habbos_tracks_is_one_the_server_counts()
    {
        await SeedAsync();
        var store = Store();

        await store.ReloadAsync(Ct);

        store
            .Tracks.SelectMany(x => x.Tasks)
            .Select(x => x.ActionType.ToLowerInvariant())
            .Distinct()
            .Should()
            .HaveCount(35)
            .And.BeSubsetOf(RewardTrackActionTypes.ALL);
        store.Counts(RewardTrackActionTypes.PET_OTHER_USERS_ROOM).Should().BeTrue();
        store.Counts("not_a_type").Should().BeFalse();
    }

    [Fact]
    public async Task A_saved_track_is_read_back_and_players_get_it_only_within_its_dates()
    {
        var store = Store();
        var start = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var track = Track("habboween2026");
        track.StartsAt = start;
        track.EndsAt = start.AddDays(14);

        await store.SaveAsync([track], Ct);

        store.Tracks.Should().ContainSingle().Which.Tasks.Should().HaveCount(2);
        store.LiveAt(start.AddSeconds(-1)).Should().BeEmpty();
        store.LiveAt(start).Should().ContainSingle();
        store.LiveAt(start.AddDays(14)).Should().BeEmpty();

        // Saved again, the track is what was saved: a task taken out is gone.
        track.Tasks.RemoveAt(1);
        await store.SaveAsync([track], Ct);
        var other = Store();
        await other.ReloadAsync(Ct);

        other.Tracks.Should().ContainSingle().Which.Tasks.Should().ContainSingle();
        other.Tracks[0].StartsAt.Should().Be(start);
        other
            .Tracks[0]
            .Prizes.Should()
            .ContainSingle()
            .Which.ProductType.Should()
            .Be(ProductDisplayType.Badge);
    }

    [Fact]
    public async Task An_invalid_track_is_refused_and_nothing_is_saved()
    {
        var store = Store();
        var track = Track("t");
        track.Tasks[0].Levels =
        [
            new() { RequiredCount = 5, Points = 1 },
            new() { RequiredCount = 2, Points = 1 },
        ];

        await store
            .Invoking(x => x.SaveAsync([track], Ct))
            .Should()
            .ThrowAsync<ArgumentException>();

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.RewardTracks.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task A_configured_track_the_database_lacks_is_moved_in_and_the_databases_own_wins()
    {
        await SeedAsync();
        var custom = Track("custom");
        var introduction = Track("introduction");
        _config = new() { Tracks = [introduction, custom] };
        var store = Store();

        await store.ReloadAsync(Ct);

        store.Tracks.Select(x => x.Id).Should().Equal("introduction", "habboween2026", "custom");
        store.Tracks[0].Tasks.Should().HaveCount(30, "the database's Introduction is kept");
        (await store.DeleteAsync("custom", Ct)).Should().BeTrue();
        await store.ReloadAsync(Ct);
        store.Tracks.Select(x => x.Id).Should().NotContain("custom");
    }

    [Fact]
    public async Task A_disabled_track_counts_nothing_and_is_never_live()
    {
        var store = Store();
        var track = Track("t");
        track.Enabled = false;

        await store.SaveAsync([track], Ct);

        store.Counts(RewardTrackActionTypes.WAVE).Should().BeFalse();
        store.LiveAt(DateTime.UtcNow).Should().BeEmpty();
    }

    internal static RewardTrackDefinition Track(string id) =>
        new()
        {
            Id = id,
            Theme = "red",
            Premium = new()
            {
                TaskPointsBoost = 1.2,
                CostCredits = 75,
                CostDiamonds = 75,
            },
            Tasks =
            [
                new()
                {
                    Id = "wave",
                    ActionType = RewardTrackActionTypes.WAVE,
                    Levels = [new() { RequiredCount = 1, Points = 10 }],
                },
                new()
                {
                    Id = "dance",
                    ActionType = RewardTrackActionTypes.DANCE,
                    Levels =
                    [
                        new() { RequiredCount = 1, Points = 10 },
                        new() { RequiredCount = 5, Points = 20 },
                    ],
                },
            ],
            Prizes =
            [
                new()
                {
                    Id = "badge",
                    RequiredPoints = 10,
                    ProductType = ProductDisplayType.Badge,
                    RewardTypeId = "DAD18",
                },
            ],
        };

    private RewardTrackStore Store() =>
        new(_db, Options.Create(_config), NullLogger<RewardTrackStore>.Instance);

    /// <summary>Runs the seed of the migration that made the tables, as MySQL would.</summary>
    private async Task SeedAsync()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        typeof(Migration)
            .GetMethod(
                "Up",
                BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(MigrationBuilder)]
            )!
            .Invoke(new AddRewardTrackTables(), [builder]);

        await using var db = await _db.CreateDbContextAsync(Ct);

        foreach (var sql in builder.Operations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(sql.Sql, Ct);
    }
}
