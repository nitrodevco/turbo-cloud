using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>
/// The credit vault's withdraw. No credit waits in a credit vault here (see
/// <see cref="CreditVaultStatusMessageHandler"/>), so there is nothing to move to the purse; Flash
/// draws no withdraw button (<c>vault_view.xml</c> has no <c>vaultWithdraw_button</c>) and listens
/// for no answer to it.
/// </summary>
public class WithdrawCreditVaultMessageHandler : IMessageHandler<WithdrawCreditVaultMessage>
{
    public async ValueTask HandleAsync(
        WithdrawCreditVaultMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
