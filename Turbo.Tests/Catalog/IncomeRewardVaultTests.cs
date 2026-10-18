using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Vault;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Vault.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Earnings paid into the vault itself. The official client capture (2026-10-13/16) lists them per
/// category in IncomeRewardStatus (daily gift 5; achievements 20, 50, 50, ...; level progression
/// 40, 40, all type 0) and announces each with IncomeRewardNotification {category}; a claim pays a
/// category into the purse (Flash's EarningsView: type 0 into the duckets, type 1 into the credits).
/// </summary>
public sealed class IncomeRewardVaultTests : IDisposable
{
    private const int PLAYER = 7;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly List<(CurrencyKind Kind, int Amount, string Reference)> _credited = [];
    private readonly HashSet<string> _references = [];
    private readonly HeldTimers _timers = new();
    private readonly IncomeRewardService _vault;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public IncomeRewardVaultTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "earner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _fakes.Handlers[nameof(IPlayerWalletGrain.CreditAsync)] = call =>
        {
            var reference = (string)call.Args[2]!;

            if (!_references.Add(reference))
                return Task.FromResult(WalletCreditResult.AlreadyApplied);

            _credited.Add(((CurrencyKind)call.Args[0]!, (int)call.Args[1]!, reference));

            return Task.FromResult(WalletCreditResult.Applied);
        };
        _vault = new IncomeRewardService(
            [],
            _db,
            _fakes.Create<IGrainFactory>(),
            _timers,
            Options.Create(new CatalogConfig()),
            NullLogger<IncomeRewardService>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    private Task<bool> DepositAsync(
        VaultRewardCategoryType category,
        int amount,
        string reference
    ) => _vault.DepositAsync(PLAYER, category, VaultRewardType.Duckets, amount, reference, Ct);

    private async Task<List<(VaultRewardCategoryType, VaultRewardType, int)>> WaitingAsync() =>
        [
            .. (await _vault.GetRewardsAsync(PLAYER, Ct)).Select(x =>
                (x.RewardCategory, x.RewardType, x.Amount)
            ),
        ];

    [Fact]
    public async Task Deposits_wait_per_category_and_a_claim_pays_its_category_into_the_purse()
    {
        Assert.True(await DepositAsync(VaultRewardCategoryType.LevelProgression, 40, "level:2"));
        Assert.True(await DepositAsync(VaultRewardCategoryType.Achievements, 20, "ach:1"));
        Assert.True(await DepositAsync(VaultRewardCategoryType.Achievements, 50, "ach:2"));
        // A payer's retry holds nothing more and announces nothing more.
        Assert.True(await DepositAsync(VaultRewardCategoryType.Achievements, 50, "ach:2"));
        Assert.False(await DepositAsync(VaultRewardCategoryType.Achievements, 0, "ach:3"));

        Assert.Equal(
            [
                VaultRewardCategoryType.LevelProgression,
                VaultRewardCategoryType.Achievements,
                VaultRewardCategoryType.Achievements,
            ],
            _fakes
                .Log.Of("TrySendComposerAsync")
                .Select(x => x.Args[0])
                .OfType<IncomeRewardNotificationMessageComposer>()
                .Select(x => x.RewardCategory)
        );
        Assert.Equal(
            [
                (VaultRewardCategoryType.Achievements, VaultRewardType.Duckets, 20),
                (VaultRewardCategoryType.Achievements, VaultRewardType.Duckets, 50),
                (VaultRewardCategoryType.LevelProgression, VaultRewardType.Duckets, 40),
            ],
            await WaitingAsync()
        );

        Assert.True(await _vault.ClaimAsync(PLAYER, VaultRewardCategoryType.Achievements, Ct));
        Assert.Equal(
            [(CurrencyKind.ActivityPoints(0), 70)],
            _credited.Select(x => (x.Kind, x.Amount))
        );
        Assert.Equal(
            [(VaultRewardCategoryType.LevelProgression, VaultRewardType.Duckets, 40)],
            await WaitingAsync()
        );

        // Nothing left in the category: Flash enables its button again.
        Assert.False(await _vault.ClaimAsync(PLAYER, VaultRewardCategoryType.Achievements, Ct));

        Assert.True(await _vault.ClaimAsync(PLAYER, VaultRewardCategoryType.All, Ct));
        Assert.Equal(
            [(CurrencyKind.ActivityPoints(0), 70), (CurrencyKind.ActivityPoints(0), 40)],
            _credited.Select(x => (x.Kind, x.Amount))
        );
        Assert.Empty(await WaitingAsync());

        Assert.True(await _vault.ClaimAsync(PLAYER, VaultRewardCategoryType.All, Ct));
        Assert.Equal(2, _credited.Count);

        // A claimed row still holds its reference, so a late retry of its payer is not paid twice.
        Assert.True(await DepositAsync(VaultRewardCategoryType.Achievements, 20, "ach:1"));
        Assert.Empty(await WaitingAsync());
    }

    [Fact]
    public async Task A_claim_is_one_operation_whose_payouts_are_each_caused_by_their_category()
    {
        await DepositAsync(VaultRewardCategoryType.DailyGift, 5, "gift:1");
        await DepositAsync(VaultRewardCategoryType.HabboClub, 30, "payday:1");
        await DepositAsync(VaultRewardCategoryType.LevelProgression, 40, "level:2");

        Assert.True(await _vault.ClaimAsync(PLAYER, VaultRewardCategoryType.All, Ct));

        var causes = _fakes
            .Log.Of(nameof(IPlayerWalletGrain.CreditAsync))
            .Select(x => (MovementCause)x.Args[3]!)
            .ToList();
        Assert.Equal(
            [
                MovementCauses.VAULT_DAILY_GIFT,
                MovementCauses.VAULT_HABBO_CLUB,
                MovementCauses.VAULT_LEVEL_PROGRESSION,
            ],
            causes.Select(x => x.Cause).Order()
        );
        Assert.Equal([0, 1, 2], causes.Select(x => (int)x.FirstSeq).Order());
        Assert.Single(causes.Select(x => (x.OperationId, x.CauseRef, x.ActorPlayerId)).Distinct());
        Assert.Equal(
            MovementCauses.VAULT_CLAIM,
            MovementCauses.VaultClaimOf((VaultRewardCategoryType)99)
        );
    }

    [Fact]
    public async Task A_claim_the_purse_refused_is_retried_under_the_same_reference()
    {
        await DepositAsync(VaultRewardCategoryType.Achievements, 20, "ach:1");
        var refuse = true;
        var attempts = new List<string>();
        var credit = _fakes.Handlers[nameof(IPlayerWalletGrain.CreditAsync)];
        _fakes.Handlers[nameof(IPlayerWalletGrain.CreditAsync)] = call =>
        {
            attempts.Add((string)call.Args[2]!);

            return refuse ? Task.FromResult(WalletCreditResult.Rejected) : credit(call);
        };

        Assert.False(await _vault.ClaimAsync(PLAYER, VaultRewardCategoryType.Achievements, Ct));
        Assert.Equal(
            [(VaultRewardCategoryType.Achievements, VaultRewardType.Duckets, 20)],
            await WaitingAsync()
        );

        // A deposit after the refused claim is a claim of its own.
        await DepositAsync(VaultRewardCategoryType.Achievements, 50, "ach:2");
        refuse = false;
        Assert.True(await _vault.ClaimAsync(PLAYER, VaultRewardCategoryType.Achievements, Ct));

        Assert.Equal(attempts[0], attempts[1]);
        Assert.Equal([20, 50], _credited.Select(x => x.Amount).Order());
        Assert.Empty(await WaitingAsync());
        await using var db = await _db.CreateDbContextAsync(Ct);
        Assert.All(await db.PlayerIncomeRewards.ToListAsync(Ct), x => Assert.NotNull(x.ClaimedAt));
    }

    [Fact]
    public async Task A_burst_of_earnings_is_followed_by_one_status_nobody_asked_for()
    {
        // Official client capture 2026-10-13/17: IncomeRewardNotification {2}, {5}, {5} within 60 ms,
        // then one IncomeRewardStatus 117 ms after the first, with no request from the client.
        await DepositAsync(VaultRewardCategoryType.Achievements, 20, "ach:1");
        await DepositAsync(VaultRewardCategoryType.LevelProgression, 40, "level:2");
        await DepositAsync(VaultRewardCategoryType.LevelProgression, 45, "level:3");

        Assert.Empty(Sent().OfType<IncomeRewardStatusMessageComposer>());
        Assert.Equal(1, _timers.Fire());

        var status = await StatusAsync(1);
        Assert.Equal(
            [
                (VaultRewardCategoryType.Achievements, VaultRewardType.Duckets, 20),
                (VaultRewardCategoryType.LevelProgression, VaultRewardType.Duckets, 40),
                (VaultRewardCategoryType.LevelProgression, VaultRewardType.Duckets, 45),
            ],
            status.IncomeRewards.Select(x => (x.RewardCategory, x.RewardType, x.Amount))
        );
        Assert.Equal(4, Sent().Count);

        // A later earning has a status of its own.
        await DepositAsync(VaultRewardCategoryType.Achievements, 50, "ach:2");
        Assert.Equal(1, _timers.Fire());

        Assert.Equal(4, (await StatusAsync(2)).IncomeRewards.Count);
        Assert.Equal(6, Sent().Count);
    }

    private List<object?> Sent() =>
        [.. _fakes.Log.Of("TrySendComposerAsync").Select(x => x.Args[0])];

    /// <summary>The <paramref name="nth"/> status sent, once the push that sends it has finished.</summary>
    private async Task<IncomeRewardStatusMessageComposer> StatusAsync(int nth)
    {
        for (var tries = 0; tries < 100; tries++)
        {
            var sent = Sent().OfType<IncomeRewardStatusMessageComposer>().ToList();

            if (sent.Count >= nth)
                return sent[nth - 1];

            await Task.Delay(50, Ct);
        }

        throw new TimeoutException($"status {nth} never sent");
    }

    /// <summary>A clock whose timers fire only when the test says so.</summary>
    private sealed class HeldTimers : TimeProvider
    {
        private readonly List<(TimerCallback Callback, object? State)> _held = [];

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period
        )
        {
            lock (_held)
                _held.Add((callback, state));

            return new Held();
        }

        /// <summary>Fires every timer made so far; how many there were.</summary>
        public int Fire()
        {
            List<(TimerCallback Callback, object? State)> due;

            lock (_held)
            {
                due = [.. _held];
                _held.Clear();
            }

            foreach (var (callback, state) in due)
                callback(state);

            return due.Count;
        }

        private sealed class Held : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() { }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
