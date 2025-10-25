using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;

namespace Turbo.PacketHandlers.Preferences;

public class SetNewNavigatorWindowPreferencesMessageHandler
    : IMessageHandler<SetNewNavigatorWindowPreferencesMessage>
{
    public async ValueTask HandleAsync(
        SetNewNavigatorWindowPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
