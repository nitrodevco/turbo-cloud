using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class ToggleStaffPickMessageHandler : IMessageHandler<ToggleStaffPickMessage>
{
    public async ValueTask HandleAsync(
        ToggleStaffPickMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
