using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Competition;
using Turbo.Primitives.Messages.Incoming.Competition;
using Turbo.Primitives.Messages.Outgoing.Competition;

namespace Turbo.PacketHandlers.Competition;

public class GetCurrentTimingCodeMessageHandler : IMessageHandler<GetCurrentTimingCodeMessage>
{
    public async ValueTask HandleAsync(
        GetCurrentTimingCodeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new CurrentTimingCodeMessageComposer
                {
                    SchedulingStr = message.SlotConfig,
                    Code = ReceptionSchedule.GetCurrentCode(
                        message.SlotConfig,
                        DateTimeOffset.UtcNow
                    ),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
