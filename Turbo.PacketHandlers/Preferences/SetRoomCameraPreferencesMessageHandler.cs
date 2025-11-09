using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;

namespace Turbo.PacketHandlers.Preferences;

public class SetRoomCameraPreferencesMessageHandler
    : IMessageHandler<SetRoomCameraPreferencesMessage>
{
    public async ValueTask HandleAsync(
        SetRoomCameraPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
