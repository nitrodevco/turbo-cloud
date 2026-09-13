using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

public class SetRoomCameraPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetRoomCameraPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetRoomCameraPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetRoomCameraFollowDisabledAsync(message.CameraFollowDisabled, ct)
            .ConfigureAwait(false);
    }
}
