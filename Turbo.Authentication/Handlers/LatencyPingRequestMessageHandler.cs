using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Messages.Outgoing.Tracking;

namespace Turbo.Authentication.Handlers;

public class LatencyPingRequestMessageHandler() : IMessageHandler<LatencyPingRequestMessage>
{
    public async ValueTask HandleAsync(
        LatencyPingRequestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx
            .Session.SendComposerAsync(
                new LatencyPingResponseMessage { RequestId = message.RequestId },
                ct
            )
            .ConfigureAwait(false);
    }
}
