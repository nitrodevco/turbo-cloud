using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class AddAdminRightsToMemberMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AddAdminRightsToMemberMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AddAdminRightsToMemberMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0 || message.PlayerId <= 0)
            return;

        var result = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .SetAdminAsync(ctx.PlayerId, message.PlayerId, true, ct)
            .ConfigureAwait(false);

        if (result.Failure is { } reason)
        {
            await ctx.SendComposerAsync(
                    new GuildMemberMgmtFailedMessageComposer
                    {
                        GuildId = message.GuildId,
                        Reason = reason,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
