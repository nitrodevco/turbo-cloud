using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

public class CreditVaultStatusMessageHandler : IMessageHandler<CreditVaultStatusMessage>
{
    public async ValueTask HandleAsync(
        CreditVaultStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
