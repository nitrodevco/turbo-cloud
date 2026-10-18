using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>
/// The credit vault's status (the old taxed vault, not the earnings vault). Credits are paid into
/// the purse here, so no credit ever waits in it: the answer is the locked, empty vault the
/// official server sends to an account with nothing in it (official client capture 2026-10-13).
/// Flash still parses it (<c>CreditVaultStatusMessageEventParser</c>: unlocked, total, withdrawable)
/// but no longer asks for it or draws it: <c>getCreditVaultStatus</c> has no caller and
/// <c>vault_view.xml</c> has no vault section.
/// </summary>
public class CreditVaultStatusMessageHandler : IMessageHandler<CreditVaultStatusMessage>
{
    public async ValueTask HandleAsync(
        CreditVaultStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new CreditVaultStatusMessageComposer
                {
                    IsUnlocked = false,
                    TotalBalance = 0,
                    WithdrawBalance = 0,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
