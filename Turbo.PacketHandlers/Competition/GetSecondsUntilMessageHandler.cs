using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Competition;
using Turbo.Primitives.Messages.Incoming.Competition;
using Turbo.Primitives.Messages.Outgoing.Competition;

namespace Turbo.PacketHandlers.Competition;

public class GetSecondsUntilMessageHandler : IMessageHandler<GetSecondsUntilMessage>
{
    public async ValueTask HandleAsync(
        GetSecondsUntilMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new SecondsUntilMessageComposer
                {
                    TimeStr = message.TimeStr,
                    SecondsUntil = ReceptionSchedule.GetSecondsUntil(
                        message.TimeStr,
                        DateTimeOffset.UtcNow
                    ),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
