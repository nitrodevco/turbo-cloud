using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateVariableMessageHandler : IMessageHandler<UpdateVariableMessage>
{
    public async ValueTask HandleAsync(
        UpdateVariableMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
