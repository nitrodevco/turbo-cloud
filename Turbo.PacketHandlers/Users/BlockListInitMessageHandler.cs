using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Consumes the request until the block list is sent. <c>PlayerMessengerGrain</c> holds the
/// blocked ids, but nothing reads them out for the client yet (no grain method returns them).
/// </summary>
public class BlockListInitMessageHandler : IMessageHandler<BlockListInitMessage>
{
    public async ValueTask HandleAsync(
        BlockListInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
