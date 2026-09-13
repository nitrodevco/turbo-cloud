using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

public class SetSoundSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetSoundSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetSoundSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetSoundSettingsAsync(
                message.GenericVolume,
                message.FurniVolume,
                message.TraxVolume,
                ct
            )
            .ConfigureAwait(false);
    }
}
