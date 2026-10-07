using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Inventory;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// The effects grain on a real (SQLite) database and a clock the test moves: what a player is
/// given, what activating uses, what may be worn, and what runs out when.
/// </summary>
public sealed class PlayerEffectGrainTests : IDisposable
{
    private const int PLAYER_ID = 1;
    private const int DURATION = 600;
    private const int EFFECT = 7;
    private const int OTHER_EFFECT = 9;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(
        new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero)
    );

    private int _rowId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerEffectGrainTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER_ID,
                Name = "effects",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(object Grain, IPlayerEffectGrain Effects)> NewGrainAsync(
        EffectConfig? config = null
    )
    {
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.Effects.PlayerEffectGrain",
            _fakes,
            _db,
            PLAYER_ID
        );

        RoomHarness.SetField(grain, "_time", _clock);
        RoomHarness.SetField(
            grain,
            "_config",
            config ?? new EffectConfig { DefaultDurationSeconds = DURATION }
        );

        await ((Orleans.Grain)grain).OnActivateAsync(Ct);

        return (grain, (IPlayerEffectGrain)grain);
    }

    /// <summary>The expiry timer fires: the harness makes timers inert, so a test calls its tick.</summary>
    private static Task TickAsync(object grain) =>
        (Task)
            grain
                .GetType()
                .GetMethod("OnExpiryTimerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(grain, [Ct])!;

    private static EffectGrantRequest Request(int effectId, int copies) =>
        new() { EffectId = effectId, Copies = copies };

    private IEnumerable<T> Sent<T>()
        where T : class =>
        _fakes.Log.Of("SendComposerAsync").Select(call => call.Args[0]).OfType<T>();

    private IReadOnlyList<(int EffectId, ImmutableArray<int> Owned)> WornCalls() =>
        [
            .. _fakes
                .Log.Of(nameof(IPlayerPresenceGrain.OnWornEffectChangedAsync))
                .Select(call => ((int)call.Args[0]!, (ImmutableArray<int>)call.Args[1]!)),
        ];

    private async Task<List<PlayerEffectEntity>> RowsAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.PlayerEffects!.AsNoTracking().OrderBy(x => x.EffectId).ToListAsync(Ct);
    }

    private void InsertRow(
        int effectId,
        int inactive = 0,
        bool permanent = false,
        DateTime? expiresAt = null
    ) =>
        _db.Insert(
            new PlayerEffectEntity
            {
                Id = ++_rowId,
                PlayerEntityId = PLAYER_ID,
                EffectId = effectId,
                InactiveCount = inactive,
                IsPermanent = permanent,
                ExpiresAt = expiresAt,
                PlayerEntity = null!,
            }
        );

    [Fact]
    public async Task GivingACopyStoresItAndTellsThePlayerOncePerCopy()
    {
        var (_, effects) = await NewGrainAsync();

        (await effects.GiveEffectAsync(EFFECT, 0, 3, false, Ct))
            .Should()
            .Be(EffectGrantResult.Granted);

        var row = (await RowsAsync()).Single();
        row.EffectId.Should().Be(EFFECT);
        row.InactiveCount.Should().Be(3);
        row.ExpiresAt.Should().BeNull();
        Sent<AvatarEffectAddedMessageComposer>()
            .Should()
            .HaveCount(3)
            .And.OnlyContain(x => x.Type == EFFECT && x.Duration == DURATION && !x.IsPermanent);
    }

    [Fact]
    public async Task CopiesOfOneEffectStackInOneRow()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct);
        await effects.GiveEffectAsync(EFFECT, 0, 5, false, Ct);

        (await RowsAsync()).Single().InactiveCount.Should().Be(7);
    }

    [Fact]
    public async Task AnEffectOutsideTheHotelsRangeOrWithNoCopiesIsInvalid()
    {
        var (_, effects) = await NewGrainAsync();

        (await effects.GiveEffectAsync(0, 0, 1, false, Ct)).Should().Be(EffectGrantResult.Invalid);
        (await effects.GiveEffectAsync(-4, 0, 1, false, Ct)).Should().Be(EffectGrantResult.Invalid);
        (await effects.GiveEffectAsync(new EffectConfig().MaxEffectId + 1, 0, 1, false, Ct))
            .Should()
            .Be(EffectGrantResult.Invalid);
        (await effects.GiveEffectAsync(EFFECT, 0, 0, false, Ct))
            .Should()
            .Be(EffectGrantResult.Invalid);

        (await RowsAsync()).Should().BeEmpty();
        Sent<AvatarEffectAddedMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task CopiesPastTheCapAreRefusedAndNothingIsChanged()
    {
        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, MaxCopiesPerType = 3 }
        );

        await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct);

        (await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);
        (await RowsAsync()).Single().InactiveCount.Should().Be(2);
    }

    [Fact]
    public async Task TheDistinctEffectCapStopsANewEffectButNotMoreOfOneOwned()
    {
        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, MaxDistinctEffects = 1 }
        );

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);

        (await effects.GiveEffectAsync(OTHER_EFFECT, 0, 1, false, Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);
        (await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct))
            .Should()
            .Be(EffectGrantResult.Granted);
    }

    [Fact]
    public async Task CheckingAGrantChangesNothing()
    {
        var (_, effects) = await NewGrainAsync();

        (await effects.CheckGiveEffectsAsync([Request(EFFECT, 2)], Ct))
            .Should()
            .Be(EffectGrantResult.Granted);
        (await effects.CheckGiveEffectsAsync([Request(0, 2)], Ct))
            .Should()
            .Be(EffectGrantResult.Invalid);

        (await RowsAsync()).Should().BeEmpty();
        Sent<AvatarEffectAddedMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task APermanentEffectReplacesTheCopiesAndTheRunningOneAndRefusesMoreTimedOnes()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);

        (await effects.GiveEffectAsync(EFFECT, 0, 1, true, Ct))
            .Should()
            .Be(EffectGrantResult.Granted);

        var row = (await RowsAsync()).Single();
        row.IsPermanent.Should().BeTrue();
        row.InactiveCount.Should().Be(0);
        row.ExpiresAt.Should().BeNull();

        (await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct))
            .Should()
            .Be(EffectGrantResult.AlreadyPermanent);
        (await effects.CheckGiveEffectsAsync([Request(EFFECT, 1)], Ct))
            .Should()
            .Be(EffectGrantResult.AlreadyPermanent);
    }

    [Fact]
    public async Task ActivatingUsesOneCopyStartsTheTimerAndWearsTheEffect()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct);

        (await effects.ActivateEffectAsync(EFFECT, Ct)).Should().BeTrue();

        var row = (await RowsAsync()).Single();
        row.InactiveCount.Should().Be(1);
        row.ExpiresAt.Should()
            .BeCloseTo(
                _clock.GetUtcNow().UtcDateTime.AddSeconds(DURATION),
                TimeSpan.FromSeconds(1)
            );
        Sent<AvatarEffectActivatedMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<AvatarEffectActivatedMessageComposer>(x =>
                x.Type == EFFECT && x.Duration == DURATION && !x.IsPermanent
            );
        WornCalls().Should().ContainSingle().Which.EffectId.Should().Be(EFFECT);
    }

    [Fact]
    public async Task ActivatingAnEffectThatIsAlreadyRunningOnlyWearsItAndUsesNoSecondCopy()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);

        // The client activates again on every room it enters.
        (await effects.ActivateEffectAsync(EFFECT, Ct))
            .Should()
            .BeTrue();

        (await RowsAsync()).Single().InactiveCount.Should().Be(1);
        Sent<AvatarEffectActivatedMessageComposer>().Should().ContainSingle();
        WornCalls().Should().HaveCount(2);
    }

    [Fact]
    public async Task ActivatingAnEffectThePlayerDoesNotHaveDoesNothing()
    {
        var (_, effects) = await NewGrainAsync();

        (await effects.ActivateEffectAsync(EFFECT, Ct)).Should().BeFalse();
        (await effects.ActivateEffectAsync(-1, Ct)).Should().BeFalse();

        WornCalls().Should().BeEmpty();
        Sent<AvatarEffectActivatedMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyAnEffectThatIsRunningOrPermanentCanBeSelected()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.GiveEffectAsync(OTHER_EFFECT, 0, 1, true, Ct);

        // A copy that waits is not worn until it is activated.
        (await effects.SelectEffectAsync(EFFECT, Ct))
            .Should()
            .BeFalse();
        (await effects.SelectEffectAsync(OTHER_EFFECT, Ct)).Should().BeTrue();
        (await effects.SelectEffectAsync(12345, Ct)).Should().BeFalse();

        await effects.ActivateEffectAsync(EFFECT, Ct);
        (await effects.SelectEffectAsync(EFFECT, Ct)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SelectingZeroOrLessTakesOffWhateverOfTheirsIsWorn(int unwear)
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.GiveEffectAsync(OTHER_EFFECT, 0, 1, true, Ct);

        (await effects.SelectEffectAsync(unwear, Ct)).Should().BeTrue();

        var call = WornCalls().Single();
        call.EffectId.Should().Be(0);
        call.Owned.Should().BeEquivalentTo([EFFECT, OTHER_EFFECT]);
    }

    [Fact]
    public async Task WearingIsToldWithEveryEffectThePlayerOwnsSoTheRoomCanSwitchBetweenThem()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.GiveEffectAsync(OTHER_EFFECT, 0, 1, true, Ct);
        await effects.SelectEffectAsync(OTHER_EFFECT, Ct);

        WornCalls().Single().Owned.Should().BeEquivalentTo([EFFECT, OTHER_EFFECT]);
    }

    [Fact]
    public async Task WhenTheTimerFiresTheLastCopyIsGoneTheRowIsDeletedAndOnlyThatEffectIsTakenOff()
    {
        var (grain, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.GiveEffectAsync(OTHER_EFFECT, 0, 1, true, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);

        _clock.Advance(TimeSpan.FromSeconds(DURATION + 1));
        await TickAsync(grain);

        (await RowsAsync()).Select(x => x.EffectId).Should().Equal(OTHER_EFFECT);
        Sent<AvatarEffectExpiredMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Type.Should()
            .Be(EFFECT);

        // Not every effect the player owns: the permanent one they may be wearing stays on.
        var takeOff = WornCalls().Last();
        takeOff.EffectId.Should().Be(0);
        takeOff.Owned.Should().Equal(EFFECT);
    }

    [Fact]
    public async Task AStackKeepsItsOtherCopiesWaitingWhenTheRunningOneEnds()
    {
        var (grain, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 3, false, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);

        _clock.Advance(TimeSpan.FromSeconds(DURATION + 1));
        await TickAsync(grain);

        var row = (await RowsAsync()).Single();
        row.InactiveCount.Should().Be(2);
        row.ExpiresAt.Should().BeNull();

        var entry = (await effects.GetEffectsAsync(Ct)).Single();
        entry.InactiveEffectsInInventory.Should().Be(2);
        entry.SecondsLeftIfActive.Should().Be(-1);
    }

    [Fact]
    public async Task NothingExpiresBeforeItsTime()
    {
        var (grain, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);

        _clock.Advance(TimeSpan.FromSeconds(DURATION - 1));
        await TickAsync(grain);

        (await RowsAsync()).Should().ContainSingle();
        Sent<AvatarEffectExpiredMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task ACopyThatRanOutIsGoneEvenIfTheTimerNeverFired()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);

        _clock.Advance(TimeSpan.FromSeconds(DURATION + 5));

        // The list already leaves it out, and the next request finds it ended.
        (await effects.GetEffectsAsync(Ct))
            .Should()
            .BeEmpty();
        (await effects.SelectEffectAsync(EFFECT, Ct)).Should().BeFalse();

        (await RowsAsync()).Should().BeEmpty();
        Sent<AvatarEffectExpiredMessageComposer>().Should().ContainSingle();
    }

    [Fact]
    public async Task ARunningEffectSurvivesARestartWithTheTimeThatIsLeft()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);
        _clock.Advance(TimeSpan.FromSeconds(200));

        var (_, afterRestart) = await NewGrainAsync();

        var entry = (await afterRestart.GetEffectsAsync(Ct)).Single();
        entry.SecondsLeftIfActive.Should().Be(DURATION - 200);
        entry.Duration.Should().Be(DURATION);
    }

    [Fact]
    public async Task CopiesThatRanOutWhileTheGrainWasAwayAreDroppedQuietly()
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        InsertRow(EFFECT, inactive: 0, expiresAt: now.AddMinutes(-5));
        InsertRow(OTHER_EFFECT, inactive: 2, expiresAt: now.AddMinutes(-5));

        var (_, effects) = await NewGrainAsync();

        // The last copy of one is gone; the other falls back to waiting.
        var rows = await RowsAsync();
        rows.Select(x => x.EffectId).Should().Equal(OTHER_EFFECT);
        rows[0].InactiveCount.Should().Be(2);
        rows[0].ExpiresAt.Should().BeNull();

        // Nobody was shown them running, so nobody is told they ended.
        Sent<AvatarEffectExpiredMessageComposer>().Should().BeEmpty();
        WornCalls().Should().BeEmpty();
        (await effects.GetEffectsAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task TheListUsesTheValuesTheClientReads()
    {
        var (_, effects) = await NewGrainAsync(
            new EffectConfig
            {
                DefaultDurationSeconds = DURATION,
                DurationOverrides = { [OTHER_EFFECT] = 90 },
            }
        );

        await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct);
        await effects.GiveEffectAsync(OTHER_EFFECT, 1, 1, false, Ct);
        await effects.GiveEffectAsync(11, 0, 1, true, Ct);
        await effects.ActivateEffectAsync(OTHER_EFFECT, Ct);

        var list = await effects.GetEffectsAsync(Ct);

        list.Select(x => x.Type).Should().Equal(EFFECT, OTHER_EFFECT, 11);

        // Waiting: minus one is the client's "not running", and zero is never sent.
        list[0]
            .Should()
            .Match<AvatarEffectSnapshot>(x =>
                x.InactiveEffectsInInventory == 2
                && x.SecondsLeftIfActive == -1
                && x.Duration == DURATION
                && !x.IsPermanent
            );

        // Running: the per-effect duration, a costume's sub type, and time left of at least one.
        list[1]
            .Should()
            .Match<AvatarEffectSnapshot>(x =>
                x.SubType == 1
                && x.Duration == 90
                && x.InactiveEffectsInInventory == 0
                && x.SecondsLeftIfActive == 90
                && !x.IsPermanent
            );

        // Permanent: shown as running with a full bar.
        list[2]
            .Should()
            .Match<AvatarEffectSnapshot>(x =>
                x.IsPermanent
                && x.SecondsLeftIfActive == DURATION
                && x.InactiveEffectsInInventory == 0
            );
    }

    [Fact]
    public async Task ARunningEffectWithUnderASecondLeftIsNeverShownAsZero()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.ActivateEffectAsync(EFFECT, Ct);
        _clock.Advance(TimeSpan.FromMilliseconds((DURATION * 1000) - 300));

        (await effects.GetEffectsAsync(Ct)).Single().SecondsLeftIfActive.Should().Be(1);
    }

    [Fact]
    public async Task AnEffectTheHotelNamesAsACostumeIsStoredAsOneWhateverTheCallerSays()
    {
        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, CostumeEffectIds = { EFFECT } }
        );

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);
        await effects.GiveEffectAsync(OTHER_EFFECT, 0, 1, false, Ct);

        var rows = await RowsAsync();
        rows.Single(x => x.EffectId == EFFECT).SubType.Should().Be(1);
        rows.Single(x => x.EffectId == OTHER_EFFECT).SubType.Should().Be(0);
        Sent<AvatarEffectAddedMessageComposer>()
            .Single(x => x.Type == EFFECT)
            .SubType.Should()
            .Be(1);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MaxValue - 1)]
    public async Task AQuantityThatWouldWrapTheCountNegativeIsRefusedNotStored(int copies)
    {
        var (_, effects) = await NewGrainAsync();

        // The player holds a copy already: 1 + int.MaxValue wraps to a negative number in an int,
        // which used to pass the cap and be written to the database.
        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);

        (await effects.GiveEffectAsync(EFFECT, 0, copies, false, Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);
        (await effects.CheckGiveEffectsAsync([Request(EFFECT, copies)], Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);

        (await RowsAsync()).Single().InactiveCount.Should().Be(1);
        Sent<AvatarEffectAddedMessageComposer>().Should().ContainSingle();
    }

    [Fact]
    public async Task ACopyCountAboveTheCapIsRefusedForANewEffectToo()
    {
        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, MaxCopiesPerType = 5 }
        );

        (await effects.GiveEffectAsync(EFFECT, 0, int.MaxValue, false, Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);

        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CopiesOfOneEffectInOneCheckAreAddedUpBeforeTheCap()
    {
        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, MaxCopiesPerType = 5 }
        );

        (await effects.CheckGiveEffectsAsync([Request(EFFECT, 3), Request(EFFECT, 3)], Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);
        (await effects.CheckGiveEffectsAsync([Request(EFFECT, 3), Request(EFFECT, 2)], Ct))
            .Should()
            .Be(EffectGrantResult.Granted);
    }

    [Fact]
    public async Task TwoNewEffectsThatFitOneByOneButNotTogetherAreRefusedBeforeEitherIsGiven()
    {
        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, MaxDistinctEffects = 2 }
        );

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);

        // One more different effect fits; two do not. Judged one at a time both would pass, and the
        // second would fail after the first was given and the buyer's money taken.
        (await effects.CheckGiveEffectsAsync([Request(OTHER_EFFECT, 1)], Ct))
            .Should()
            .Be(EffectGrantResult.Granted);
        (await effects.CheckGiveEffectsAsync([Request(OTHER_EFFECT, 1), Request(11, 1)], Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);
    }

    [Fact]
    public async Task AnEffectTheHotelAppliesItselfCannotBeGiven()
    {
        var (_, effects) = await NewGrainAsync();

        // The room cannot tell where a worn effect came from, so an owned 77 (riding) could be
        // taken off a rider by the player's own unwear, or by its expiry.
        foreach (
            var reserved in new[]
            {
                28,
                29,
                30,
                33,
                34,
                35,
                36,
                38,
                39,
                77,
                95,
                96,
                97,
                98,
                184,
                185,
                218,
            }
        )
        {
            (await effects.GiveEffectAsync(reserved, 0, 1, false, Ct))
                .Should()
                .Be(EffectGrantResult.Invalid);
            (await effects.CheckGiveEffectsAsync([Request(reserved, 1)], Ct))
                .Should()
                .Be(EffectGrantResult.Invalid);
        }

        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task TheHotelsOwnReservedIdsAreAddedToTheDefaultsNeverInstead()
    {
        // A set the config binder fills adds to the defaults, so a hotel can reserve its freeze ids
        // but cannot, by listing some, un-reserve the rider's.
        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, ReservedEffectIds = { 218 } }
        );

        (await effects.GiveEffectAsync(218, 0, 1, false, Ct))
            .Should()
            .Be(EffectGrantResult.Invalid);
        (await effects.GiveEffectAsync(77, 0, 1, false, Ct)).Should().Be(EffectGrantResult.Invalid);
        (await effects.GiveEffectAsync(OTHER_EFFECT, 0, 1, false, Ct))
            .Should()
            .Be(EffectGrantResult.Granted);
    }

    [Fact]
    public async Task ALapsedCopyDoesNotCountAgainstTheCapsWhenChecking()
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        // One effect whose only copy ended a minute ago and was never cleared: as good as gone.
        InsertRow(EFFECT, inactive: 0, expiresAt: now.AddMinutes(10));

        var (_, effects) = await NewGrainAsync(
            new EffectConfig { DefaultDurationSeconds = DURATION, MaxDistinctEffects = 1 }
        );

        (await effects.CheckGiveEffectsAsync([Request(OTHER_EFFECT, 1)], Ct))
            .Should()
            .Be(EffectGrantResult.LimitReached);

        // The check is read-only and cannot clear it, so it looks at the clock instead.
        _clock.Advance(TimeSpan.FromMinutes(11));

        (await effects.CheckGiveEffectsAsync([Request(OTHER_EFFECT, 1)], Ct))
            .Should()
            .Be(EffectGrantResult.Granted);
    }

    [Fact]
    public async Task AFailedMessageNeverFailsAGrantThatIsAlreadyStored()
    {
        _fakes.Handlers["SendComposerToPlayerAsync"] = _ =>
            throw new InvalidOperationException("the session is gone");

        var (_, effects) = await NewGrainAsync();

        (await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct))
            .Should()
            .Be(EffectGrantResult.Granted);

        (await RowsAsync()).Single().InactiveCount.Should().Be(2);
    }

    [Fact]
    public async Task ARunningCopyStillHasATimerWhenTheMessageAboutItFails()
    {
        var (grain, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 1, false, Ct);

        _fakes.Handlers["SendComposerToPlayerAsync"] = _ =>
            throw new InvalidOperationException("the session is gone");

        (await effects.ActivateEffectAsync(EFFECT, Ct)).Should().BeTrue();

        var timer = grain
            .GetType()
            .GetField("_expiryTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(grain);

        timer
            .Should()
            .NotBeNull("the copy runs out at its time even though nobody could be told it started");
    }

    [Fact]
    public async Task ARowRemovedBehindTheGrainsBackIsForgottenNotBelieved()
    {
        var (_, effects) = await NewGrainAsync();

        await effects.GiveEffectAsync(EFFECT, 0, 2, false, Ct);

        // An admin tool, or a deleted player, takes the row away.
        await using (var db = await _db.CreateDbContextAsync(Ct))
            await db.PlayerEffects!.ExecuteDeleteAsync(Ct);

        var activating = () => effects.ActivateEffectAsync(EFFECT, Ct);

        await activating.Should().ThrowAsync<InvalidOperationException>();

        // Memory no longer lists what the database does not hold.
        (await effects.GetEffectsAsync(Ct))
            .Should()
            .BeEmpty();
        (await effects.ActivateEffectAsync(EFFECT, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task EachPlayersEffectsAreTheirsAlone()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 2,
                Name = "someone-else",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Female,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new PlayerEffectEntity
            {
                Id = ++_rowId,
                PlayerEntityId = 2,
                EffectId = EFFECT,
                InactiveCount = 4,
                PlayerEntity = null!,
            }
        );

        var (_, effects) = await NewGrainAsync();

        (await effects.GetEffectsAsync(Ct)).Should().BeEmpty();
        (await effects.ActivateEffectAsync(EFFECT, Ct)).Should().BeFalse();
    }
}
