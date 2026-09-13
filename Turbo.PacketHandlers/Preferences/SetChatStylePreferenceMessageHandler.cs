using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

public class SetChatStylePreferenceMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetChatStylePreferenceMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetChatStylePreferenceMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetChatStyleAsync(message.ChatStyle, message.FontSize, ct)
            .ConfigureAwait(false);
    }
}
