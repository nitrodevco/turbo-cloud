using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Vault;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The earnings vault asks for its status every time it opens, and only an answer lets the client
/// disable the claim buttons of empty categories (<c>EarningsView.onIncomeRewardDataReceived</c>).
/// </summary>
public sealed class IncomeRewardStatusTests
{
    [Fact]
    public async Task The_vault_status_is_answered_with_nothing_to_claim()
    {
        var fakes = new Fakes();

        await new IncomeRewardStatusMessageHandler().HandleAsync(
            new IncomeRewardStatusMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        var composer = Assert.IsType<IncomeRewardStatusMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        Assert.Empty(composer.IncomeRewards);
    }
}
