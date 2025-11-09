using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateTriggerMessageHandler : IMessageHandler<UpdateTriggerMessage>
{
    public async ValueTask HandleAsync(
        UpdateTriggerMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
