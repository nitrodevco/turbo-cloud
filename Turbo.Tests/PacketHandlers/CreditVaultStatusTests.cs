using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Vault;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The credit vault's status request is answered as the official server answers it (official
/// client capture 2026-10-13: locked, 0 total, 0 withdrawable); no credit waits in it here, so a
/// withdraw has nothing to move.
/// </summary>
public sealed class CreditVaultStatusTests
{
    [Fact]
    public async Task The_credit_vault_status_is_answered_locked_and_empty()
    {
        var replies = await new PacketHarness().SendAsync(
            PacketHarness.Incoming("CreditVaultStatusMessageEvent"),
            PacketHarness.Payload(_ => { })
        );

        // CreditVaultStatusMessageEventParser.parse: isUnlocked, totalBalance, withdrawBalance.
        var packet = replies.Single();
        packet.Header.Should().Be(PacketHarness.Outgoing("CreditVaultStatusMessageComposer"));
        (packet.PopBoolean(), packet.PopInt(), packet.PopInt()).Should().Be((false, 0, 0));
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task A_withdraw_moves_nothing_and_sends_nothing()
    {
        var fakes = new Fakes();

        await new WithdrawCreditVaultMessageHandler().HandleAsync(
            new WithdrawCreditVaultMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(fakes.Sent());
    }
}
