using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Turbo;

/// <summary>
/// A client opting in to Turbo's protocol extensions. The accepted ones are kept for the session
/// and answered, then whatever an accepted extension carries is sent: the permission nodes, after
/// the answer, through the same queue so they cannot overtake it.
/// </summary>
public class TurboClientCapabilitiesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<TurboClientCapabilitiesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        TurboClientCapabilitiesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var accepted = ClientCapabilities.Negotiate(message.Capabilities);
        var presence = _grainFactory.GetPlayerPresenceGrain(ctx.PlayerId);

        await presence.SetClientCapabilitiesAsync(accepted, ct).ConfigureAwait(false);
        await presence
            .SendComposerAsync(new TurboServerCapabilitiesMessage { Capabilities = accepted }, ct)
            .ConfigureAwait(false);

        if (accepted.Any(x => x.Name == ClientCapabilities.PERMISSION_NODES))
            await _grainFactory
                .GetPlayerPermissionGrain(ctx.PlayerId)
                .SendClientStateAsync(ct)
                .ConfigureAwait(false);
    }
}
