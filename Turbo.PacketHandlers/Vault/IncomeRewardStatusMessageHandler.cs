using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>
/// The earnings vault's status: every row waiting for the player. Flash's
/// <c>EarningsView.onIncomeRewardDataReceived</c> sums them per category and disables the claim
/// buttons of empty categories (and Claim All when every one is), so an empty list is still an answer.
/// </summary>
public class IncomeRewardStatusMessageHandler(IIncomeRewardService incomeRewards)
    : IMessageHandler<IncomeRewardStatusMessage>
{
    public async ValueTask HandleAsync(
        IncomeRewardStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var rewards = await incomeRewards.GetRewardsAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IncomeRewardStatusMessageComposer { IncomeRewards = [.. rewards] },
                ct
            )
            .ConfigureAwait(false);
    }
}
