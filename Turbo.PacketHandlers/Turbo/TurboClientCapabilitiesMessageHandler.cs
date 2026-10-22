using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Availability;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Turbo;

/// <summary>
/// A client opting in to Turbo's protocol extensions. The accepted ones are kept for the session
/// and answered, then whatever an accepted extension carries is sent: the permission nodes and the
/// command tree, after the answer, through the same queue so they cannot overtake it, the
/// countdown notice up now, and the client modules. The rights and perks the client was sent at login are not sent again.
/// </summary>
public class TurboClientCapabilitiesMessageHandler(
    IGrainFactory grainFactory,
    IExtensionPacketRegistry extensions,
    ICommandTreeService commandTreeService,
    IHotelCountdownNotice countdownNotice,
    IClientModuleDirectory clientModules
) : IMessageHandler<TurboClientCapabilitiesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IExtensionPacketRegistry _extensions = extensions;
    private readonly ICommandTreeService _commandTreeService = commandTreeService;
    private readonly IHotelCountdownNotice _countdownNotice = countdownNotice;
    private readonly IClientModuleDirectory _clientModules = clientModules;

    public async ValueTask HandleAsync(
        TurboClientCapabilitiesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var accepted = ClientCapabilities.Negotiate(message.Capabilities, _extensions);
        await _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .SetClientCapabilitiesAsync(accepted, ct)
            .ConfigureAwait(false);
        // Through the presence, as what follows is: ctx's queue could let the nodes overtake it.
        await _grainFactory
            .SendComposerToPlayerAsync(
                ctx.PlayerId,
                new TurboServerCapabilitiesMessage { Capabilities = accepted },
                ct
            )
            .ConfigureAwait(false);

        if (accepted.Any(x => x.Name == ClientCapabilities.PERMISSION_NODES))
            await _grainFactory
                .GetPlayerPermissionGrain(ctx.PlayerId)
                .SendPermissionNodesAsync(ct)
                .ConfigureAwait(false);

        if (accepted.Any(x => x.Name == ClientCapabilities.CHAT_COMMANDS))
            await _commandTreeService.SendAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        if (
            accepted.Any(x => x.Name == ClientCapabilities.LIVE_NOTICES)
            && _countdownNotice.Current is { } notice
        )
            await _grainFactory
                .SendComposerToPlayerAsync(ctx.PlayerId, notice, ct)
                .ConfigureAwait(false);

        if (accepted.Any(x => x.Name == ClientCapabilities.CLIENT_MODULES))
            await _grainFactory
                .SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    new TurboClientModulesMessage { Modules = _clientModules.Modules },
                    ct
                )
                .ConfigureAwait(false);
    }
}
