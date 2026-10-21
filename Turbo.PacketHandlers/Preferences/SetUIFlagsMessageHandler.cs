using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Preferences;

public class SetUIFlagsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetUIFlagsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetUIFlagsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetUIFlagsAsync((UIFlags)message.UIFlags, ct)
            .ConfigureAwait(false);
    }
}
