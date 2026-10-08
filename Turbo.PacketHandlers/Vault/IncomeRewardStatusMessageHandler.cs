using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>
/// The earnings vault's status. The hotel keeps no income for anyone yet, so the answer is an
/// empty list - which is still an answer: Flash's <c>EarningsView.onIncomeRewardDataReceived</c>
/// only disables the claim buttons of empty categories (and Claim All when every one is) on
/// hearing it. Unanswered, the vault stayed as its layout builds it, every button enabled over
/// a row of zeros.
/// </summary>
public class IncomeRewardStatusMessageHandler : IMessageHandler<IncomeRewardStatusMessage>
{
    public async ValueTask HandleAsync(
        IncomeRewardStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new IncomeRewardStatusMessageComposer { IncomeRewards = [] },
                ct
            )
            .ConfigureAwait(false);
    }
}
