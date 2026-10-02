using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans.Runtime;
using Turbo.Achievements;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Respect;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class HumanRespectOperationGrainTests : IDisposable
{
    private const string OPERATION_ID = "human-respect-test-1";
    private const int ACTOR_ID = 101;
    private const int TARGET_ID = 202;
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SuccessfulOperationPersistsPairedFactsAndReplayReturnsSavedTotal()
    {
        var (grain, fakes) = CreateGrain(OPERATION_ID, acceptedSpend: true, targetTotal: 37);

        var first = await grain.ExecuteAsync(ACTOR_ID, TARGET_ID, Ct);
        var replay = await grain.ExecuteAsync(ACTOR_ID, TARGET_ID, Ct);

        first
            .Should()
            .Be(new HumanRespectOperationResult { Accepted = true, TargetRespectTotal = 37 });
        replay.Should().Be(first);
        fakes.Log.Of(nameof(IPlayerGrain.SpendRespectOperationAsync)).Should().HaveCount(1);
        fakes.Log.Of(nameof(IPlayerGrain.ReceiveRespectOperationAsync)).Should().HaveCount(1);
        await using var db = await _db.CreateDbContextAsync(Ct);
        var operation = await db.HumanRespectOperations.SingleAsync(Ct);
        operation.Completed.Should().BeTrue();
        operation.Rejected.Should().BeFalse();
        operation.ResultTotal.Should().Be(37);
        var facts = await db.AchievementFacts.OrderBy(x => x.PlayerId).ToListAsync(Ct);
        facts.Should().HaveCount(2);
        facts
            .Select(x => (x.PlayerId, Fact: System.Text.Json.JsonDocument.Parse(x.FactJson)))
            .Select(x => (x.PlayerId, Source: x.Fact.RootElement.GetProperty("Source").GetString()))
            .Should()
            .Equal(
                (ACTOR_ID, AchievementSources.RESPECT_GIVEN),
                (TARGET_ID, AchievementSources.RESPECT_RECEIVED)
            );
    }

    [Fact]
    public async Task RejectedSpendIsDurableAndDoesNotCallRecipientOrRecordFacts()
    {
        var (grain, fakes) = CreateGrain(OPERATION_ID, acceptedSpend: false, targetTotal: 0);

        var result = await grain.ExecuteAsync(ACTOR_ID, TARGET_ID, Ct);
        var replay = await grain.ExecuteAsync(ACTOR_ID, TARGET_ID, Ct);

        result.Accepted.Should().BeFalse();
        replay.Accepted.Should().BeFalse();
        fakes.Log.Of(nameof(IPlayerGrain.SpendRespectOperationAsync)).Should().HaveCount(1);
        fakes.Log.Of(nameof(IPlayerGrain.ReceiveRespectOperationAsync)).Should().BeEmpty();
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.HumanRespectOperations.SingleAsync(Ct)).Rejected.Should().BeTrue();
        (await db.AchievementFacts.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task RecipientFailureLeavesAdmissionRecoverableWithoutPrematureFacts()
    {
        var (grain, fakes) = CreateGrain(OPERATION_ID, acceptedSpend: true, targetTotal: 37);
        fakes.Handlers[nameof(IPlayerGrain.ReceiveRespectOperationAsync)] = _ =>
            Task.FromException<int>(new InvalidOperationException("Recipient unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            grain.ExecuteAsync(ACTOR_ID, TARGET_ID, Ct)
        );
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            Assert.False((await db.HumanRespectOperations.SingleAsync(Ct)).Completed);
            Assert.Equal(0, await db.AchievementFacts.CountAsync(Ct));
        }
        var (recovered, _) = CreateGrain(OPERATION_ID, acceptedSpend: true, targetTotal: 37);
        Assert.True((await recovered.ExecuteAsync(ACTOR_ID, TARGET_ID, Ct)).Accepted);
        await using var after = await _db.CreateDbContextAsync(Ct);
        Assert.True((await after.HumanRespectOperations.SingleAsync(Ct)).Completed);
        Assert.Equal(2, await after.AchievementFacts.CountAsync(Ct));
    }

    [Fact]
    public async Task ReusingOperationIdForDifferentParticipantsIsRejectedBeforeParticipantCalls()
    {
        _db.Insert(
            new HumanRespectOperationEntity
            {
                OperationId = OPERATION_ID,
                ActorId = ACTOR_ID,
                TargetId = TARGET_ID,
                Completed = false,
                Rejected = false,
            }
        );
        var (grain, fakes) = CreateGrain(OPERATION_ID, acceptedSpend: true, targetTotal: 1);

        var act = () => grain.ExecuteAsync(ACTOR_ID, TARGET_ID + 1, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        fakes.Log.Of(nameof(IPlayerGrain.SpendRespectOperationAsync)).Should().BeEmpty();
        fakes.Log.Of(nameof(IPlayerGrain.ReceiveRespectOperationAsync)).Should().BeEmpty();
    }

    private (IHumanRespectOperationGrain Grain, Fakes Fakes) CreateGrain(
        string operationId,
        bool acceptedSpend,
        int targetTotal
    )
    {
        var fakes = new Fakes();
        fakes.Handlers["get_GrainId"] = _ =>
            GrainId.Create(GrainType.Create("human-respect-operation"), IdSpan.Create(operationId));
        fakes.Handlers[nameof(IPlayerGrain.SpendRespectOperationAsync)] = _ =>
            Task.FromResult(acceptedSpend);
        fakes.Handlers[nameof(IPlayerGrain.ReceiveRespectOperationAsync)] = _ =>
            Task.FromResult(targetTotal);

        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Respect.HumanRespectOperationGrain",
            fakes,
            _db
        );
        RoomHarness.SetField(grain, "_facts", new AchievementFactRecorder(new EmptyCatalog()));
        RoomHarness.SetField(grain, "_operationId", operationId);
        return ((IHumanRespectOperationGrain)grain, fakes);
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
