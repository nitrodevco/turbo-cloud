using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Texts;

namespace Turbo.PacketHandlers.Help;

public class GetMySanctionStatusMessageHandler(
    IGrainFactory grainFactory,
    IHotelTextProvider textProvider,
    TimeProvider timeProvider
) : IMessageHandler<GetMySanctionStatusMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IHotelTextProvider _textProvider = textProvider;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask HandleAsync(
        GetMySanctionStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var permissions = _grainFactory.GetPlayerPermissionGrain(ctx.PlayerId);
        var checks = await Task.WhenAll(
                SanctionStatuses.NODES.Select(x => permissions.ExplainAsync(x.Node, ct))
            )
            .ConfigureAwait(false);
        var sanctions = await SanctionStatuses
            .FromChecksAsync(checks, _textProvider, _timeProvider.GetUtcNow().UtcDateTime, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new SanctionStatusEventMessageComposer { Sanctions = sanctions },
                ct
            )
            .ConfigureAwait(false);
    }
}
