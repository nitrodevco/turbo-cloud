using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Groupforums;

public class ModerateMessageMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ModerateMessageMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ModerateMessageMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GroupId <= 0)
            return;

        await _grainFactory
            .GetGuildForumGrain(GuildId.Parse(message.GroupId))
            .ModerateMessageAsync(
                ctx.PlayerId,
                message.ThreadId,
                message.MessageId,
                message.State,
                ct
            )
            .ConfigureAwait(false);
    }
}
