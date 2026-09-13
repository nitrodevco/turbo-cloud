using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

public class SetChatPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetChatPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetChatPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetChatPreferencesAsync(message.ChatMode, message.BubbleWidth, message.ScrollSpeed, ct)
            .ConfigureAwait(false);
    }
}
