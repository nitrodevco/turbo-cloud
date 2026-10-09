using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Groupforums;

public class UpdateThreadMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateThreadMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateThreadMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GroupId <= 0)
            return;

        await _grainFactory
            .GetGuildForumGrain(GuildId.Parse(message.GroupId))
            .UpdateThreadAsync(
                ctx.PlayerId,
                message.ThreadId,
                message.IsSticky,
                message.IsLocked,
                ct
            )
            .ConfigureAwait(false);
    }
}
