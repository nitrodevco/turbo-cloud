using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>
/// A claim button of the earnings vault. Flash disables the button before sending
/// (<c>EarningsView.performClaim</c>) and waits for the answer: a claimed category is zeroed (Claim
/// All zeroes and disables every one), a failed one gets its button back
/// (<c>EarningsView.onIncomeRewardClaimResponse</c>, catalog/vault/EarningsView.as:66).
/// </summary>
public class IncomeRewardClaimMessageHandler(IIncomeRewardService incomeRewards)
    : IMessageHandler<IncomeRewardClaimMessage>
{
    public async ValueTask HandleAsync(
        IncomeRewardClaimMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var claimed = await incomeRewards
            .ClaimAsync(ctx.PlayerId, message.RewardCategory, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IncomeRewardClaimResponseMessageComposer
                {
                    RewardCategory = message.RewardCategory,
                    Result = claimed,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
