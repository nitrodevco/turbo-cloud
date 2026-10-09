using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Groupforums;

public class UpdateForumSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateForumSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateForumSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GroupId <= 0)
            return;

        await _grainFactory
            .GetGuildForumGrain(GuildId.Parse(message.GroupId))
            .UpdateSettingsAsync(
                ctx.PlayerId,
                message.ReadPermission,
                message.PostMessagePermission,
                message.PostThreadPermission,
                message.ModeratePermission,
                ct
            )
            .ConfigureAwait(false);
    }
}
