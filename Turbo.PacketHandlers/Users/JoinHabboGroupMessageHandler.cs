using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class JoinHabboGroupMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<JoinHabboGroupMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        JoinHabboGroupMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var result = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .JoinAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (result.Failure is { } reason)
        {
            await ctx.SendComposerAsync(
                    new HabboGroupJoinFailedMessageComposer { Reason = reason },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        // The window the player is looking at reads the group's details, not the join's answer,
        // so the refresh is what actually redraws their join button as a leave button.
        await ctx.SendComposerAsync(
                new GroupDetailsChangedMessageComposer { GuildId = message.GuildId },
                ct
            )
            .ConfigureAwait(false);
    }
}
