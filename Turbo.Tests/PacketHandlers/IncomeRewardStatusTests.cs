using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Vault;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Vault;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Vault.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The earnings vault asks for its status every time it opens, and only an answer lets the client
/// disable the claim buttons of empty categories (<c>EarningsView.onIncomeRewardDataReceived</c>);
/// a claim is always answered, or its button stays disabled (<c>EarningsView.performClaim</c>).
/// </summary>
public sealed class IncomeRewardStatusTests : IDisposable
{
    private readonly SqliteDb _db = new();

    public void Dispose() => _db.Dispose();

    private IncomeRewardService NewVault(Fakes fakes) =>
        new(
            [],
            _db,
            fakes.Create<IGrainFactory>(),
            TimeProvider.System,
            Options.Create(new CatalogConfig()),
            NullLogger<IncomeRewardService>.Instance
        );

    [Fact]
    public async Task The_vault_status_is_answered_with_nothing_to_claim()
    {
        var fakes = new Fakes();

        await new IncomeRewardStatusMessageHandler(NewVault(fakes)).HandleAsync(
            new IncomeRewardStatusMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        var composer = Assert.IsType<IncomeRewardStatusMessageComposer>(fakes.Sent().Single());
        Assert.Empty(composer.IncomeRewards);
    }

    [Theory]
    [InlineData(VaultRewardCategoryType.Achievements, false)]
    [InlineData(VaultRewardCategoryType.All, true)]
    public async Task A_claim_is_answered_for_its_category(
        VaultRewardCategoryType category,
        bool result
    )
    {
        var fakes = new Fakes();

        await new IncomeRewardClaimMessageHandler(NewVault(fakes)).HandleAsync(
            new IncomeRewardClaimMessage { RewardCategory = category },
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            new IncomeRewardClaimResponseMessageComposer
            {
                RewardCategory = category,
                Result = result,
            },
            fakes.Sent().Single()
        );
    }
}
