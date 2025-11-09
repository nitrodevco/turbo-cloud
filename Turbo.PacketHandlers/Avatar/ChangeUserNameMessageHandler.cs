using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;

namespace Turbo.PacketHandlers.Avatar;

public class ChangeUserNameMessageHandler : IMessageHandler<ChangeUserNameMessage>
{
    public async ValueTask HandleAsync(
        ChangeUserNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
