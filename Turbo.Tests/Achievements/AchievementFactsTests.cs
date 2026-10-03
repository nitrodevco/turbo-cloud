using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementFactsTests : IDisposable
{
    private readonly SqliteDb _database = new();
    private readonly Fakes _fakes = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AchievementFactsTests() =>
        _database.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "facts-test",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            }
        );

    public void Dispose()
    {
        _database.Dispose();
        GC.SuppressFinalize(this);
    }

    private AchievementFacts NewFacts() =>
        new(
            _database,
            new AchievementFactRecorder(new ListeningAchievementCatalog()),
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IHostApplicationLifetime>(),
            NullLogger<AchievementFacts>.Instance
        );

    private static AchievementFact Fact(
        string operation,
        string source = AchievementSources.FIGURE
    ) =>
        new()
        {
            OperationId = operation,
            Source = source,
            OccurredAtUtc = DateTime.UtcNow,
        };

    private int Updates() => _fakes.Log.Of(nameof(IPlayerAchievementGrain.ProcessAsync)).Count();

    private async Task WaitForUpdatesAsync(int count)
    {
        for (var i = 0; i < 200 && Updates() < count; i++)
            await Task.Delay(25, Ct);
        Assert.Equal(count, Updates());
    }

    private async Task<int> StoredAsync()
    {
        await using var db = await _database.CreateDbContextAsync(Ct);

        return await db.AchievementFacts.CountAsync(Ct);
    }

    [Fact]
    public async Task AListenedToFactIsStoredAndAsksForOneUpdate()
    {
        await NewFacts().RecordAsync(1, Fact("plugin-1"), Ct);

        Assert.Equal(1, await StoredAsync());
        await WaitForUpdatesAsync(1);
    }

    [Fact]
    public async Task RecordingTheSameOperationAgainStoresNothingMore()
    {
        var facts = NewFacts();

        await facts.RecordAsync(1, Fact("plugin-same"), Ct);
        await facts.RecordAsync(1, Fact("plugin-same"), Ct);

        Assert.Equal(1, await StoredAsync());
    }

    [Fact]
    public async Task TwoCallersRecordingTheSameOperationAtOnceBothSucceedWithOneRow()
    {
        var facts = NewFacts();

        await Task.WhenAll(
            facts.RecordAsync(1, Fact("plugin-race"), Ct),
            facts.RecordAsync(1, Fact("plugin-race"), Ct),
            facts.RecordAsync(1, Fact("plugin-race"), Ct)
        );

        Assert.Equal(1, await StoredAsync());
    }

    [Fact]
    public async Task AFactNothingListensToIsNotStoredAndAsksForNoUpdate()
    {
        await NewFacts().RecordAsync(1, Fact("plugin-quiet", "plugin.unlistened"), Ct);

        Assert.Equal(0, await StoredAsync());
        await Task.Delay(100, Ct);
        Assert.Equal(0, Updates());
    }

    [Fact]
    public async Task AnInvalidFactThrowsAndStoresNothing()
    {
        var facts = NewFacts();
        var local = Fact("plugin-local") with { OccurredAtUtc = DateTime.Now };
        var blank = Fact(" ");

        await Assert.ThrowsAsync<ArgumentException>(() => facts.RecordAsync(1, local, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => facts.RecordAsync(1, blank, Ct));
        Assert.Equal(0, await StoredAsync());
    }

    [Fact]
    public async Task TheCallerIsNeverMadeToWaitForTheUpdate()
    {
        var release = new TaskCompletionSource();
        _fakes.Handlers[nameof(IPlayerAchievementGrain.ProcessAsync)] = _ => release.Task;

        await NewFacts().RecordAsync(1, Fact("plugin-slow"), Ct);

        await WaitForUpdatesAsync(1);
        Assert.False(release.Task.IsCompleted);
        release.SetResult();
    }

    [Fact]
    public async Task FactsArrivingDuringAnUpdateShareOneFollowUp()
    {
        var release = new TaskCompletionSource();
        var calls = 0;
        _fakes.Handlers[nameof(IPlayerAchievementGrain.ProcessAsync)] = _ =>
            Interlocked.Increment(ref calls) == 1 ? release.Task : Task.CompletedTask;
        var facts = NewFacts();

        await facts.RecordAsync(1, Fact("burst-1"), Ct);
        await WaitForUpdatesAsync(1);
        await facts.RecordAsync(1, Fact("burst-2"), Ct);
        await facts.RecordAsync(1, Fact("burst-3"), Ct);
        await facts.RecordAsync(1, Fact("burst-4"), Ct);
        Assert.Equal(1, Updates());
        release.SetResult();

        await WaitForUpdatesAsync(2);
        await Task.Delay(150, Ct);
        Assert.Equal(2, Updates());
        Assert.Equal(4, await StoredAsync());
    }

    [Fact]
    public async Task AFailedUpdateDoesNotFailTheRecordAndTheNextFactAsksAgain()
    {
        var calls = 0;
        _fakes.Handlers[nameof(IPlayerAchievementGrain.ProcessAsync)] = _ =>
            Interlocked.Increment(ref calls) == 1
                ? Task.FromException(new InvalidOperationException("grain unavailable"))
                : Task.CompletedTask;
        var facts = NewFacts();

        await facts.RecordAsync(1, Fact("retry-1"), Ct);
        await WaitForUpdatesAsync(1);
        await Task.Delay(200, Ct);
        await facts.RecordAsync(1, Fact("retry-2"), Ct);

        await WaitForUpdatesAsync(2);
        Assert.Equal(2, await StoredAsync());
    }
}
