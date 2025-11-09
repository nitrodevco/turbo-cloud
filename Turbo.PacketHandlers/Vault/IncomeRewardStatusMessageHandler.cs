using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

public class IncomeRewardStatusMessageHandler : IMessageHandler<IncomeRewardStatusMessage>
{
    public async ValueTask HandleAsync(
        IncomeRewardStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
