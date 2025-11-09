using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class RemoveAdminRightsFromMemberMessageHandler
    : IMessageHandler<RemoveAdminRightsFromMemberMessage>
{
    public async ValueTask HandleAsync(
        RemoveAdminRightsFromMemberMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
