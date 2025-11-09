using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

public class IncomeRewardClaimMessageHandler : IMessageHandler<IncomeRewardClaimMessage>
{
    public async ValueTask HandleAsync(
        IncomeRewardClaimMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
