using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class ApproveAllMembershipRequestsMessageHandler
    : IMessageHandler<ApproveAllMembershipRequestsMessage>
{
    public async ValueTask HandleAsync(
        ApproveAllMembershipRequestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
