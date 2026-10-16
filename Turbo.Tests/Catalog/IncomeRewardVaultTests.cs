using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Catalog.Vault;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Messages.Outgoing.Vault;
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
            TimeProvider.System
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
                .Select(x => ((IncomeRewardNotificationMessageComposer)x.Args[0]!).RewardCategory)
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
}
