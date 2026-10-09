using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Groupforums;

public class GetThreadMessageHandler(IGrainFactory grainFactory) : IMessageHandler<GetThreadMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetThreadMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GroupId <= 0)
            return;

        await _grainFactory
            .GetGuildForumGrain(GuildId.Parse(message.GroupId))
            .SendThreadAsync(ctx.PlayerId, message.ThreadId, ct)
            .ConfigureAwait(false);
    }
}
