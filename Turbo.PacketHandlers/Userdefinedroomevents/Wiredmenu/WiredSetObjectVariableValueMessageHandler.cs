using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredSetObjectVariableValueMessageHandler
    : IMessageHandler<WiredSetObjectVariableValueMessage>
{
    public async ValueTask HandleAsync(
        WiredSetObjectVariableValueMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
