using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class PlayerSubscriptionAchievementTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerSubscriptionAchievementTests() =>
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "club-test",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            }
        );

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task OnlyPurchasedExtensionsCountAsPurchasedDaysAndEveryExtensionCountsAsGranted()
    {
        var grain = NewGrain();

        await grain.ExtendPurchasedAsync(SubscriptionType.HabboClub, 31, Ct);
        await grain.ExtendAsync(SubscriptionType.HabboClub, 10, Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var club = await db.PlayerSubscriptions.SingleAsync(Ct);
        club.TotalDaysSubscribed.Should().Be(41);
        club.PurchasedDaysSubscribed.Should().Be(31);
        // The membership is still running, so the second grant extended it from its end and
        // none of the 41 days has elapsed yet.
        AchievementStateEvaluator
            .EligibleSeconds(club.TotalDaysSubscribed, club.ExpiresAt!.Value, DateTime.UtcNow)
            .Should()
            .BeLessThan(5);
    }

    private IPlayerSubscriptionGrain NewGrain()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Subscriptions.PlayerSubscriptionGrain",
            new Fakes(),
            _db
        );
        RoomHarness.SetField(
            grain,
            "_achievementFacts",
            new AchievementFactRecorder(new EmptyCatalog())
        );
        return (IPlayerSubscriptionGrain)grain;
    }

    private sealed class EmptyCatalog : IAchievementCatalog
    {
        public ImmutableArray<AchievementDefinition> Current => [];

        public IDisposable RegisterSources(IEnumerable<AchievementSourceDefinition> sources) =>
            throw new NotSupportedException();

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ImportAsync(
            ImmutableArray<AchievementDefinition> definitions,
            bool apply,
            string actor,
            string reason,
            string operationId,
            CancellationToken ct
        ) => Task.CompletedTask;
    }
}
