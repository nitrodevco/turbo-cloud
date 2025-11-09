using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;

namespace Turbo.PacketHandlers.Avatar;

public class CheckUserNameMessageHandler : IMessageHandler<CheckUserNameMessage>
{
    public async ValueTask HandleAsync(
        CheckUserNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
