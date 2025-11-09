using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class UpdateGuildIdentityMessageHandler : IMessageHandler<UpdateGuildIdentityMessage>
{
    public async ValueTask HandleAsync(
        UpdateGuildIdentityMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
