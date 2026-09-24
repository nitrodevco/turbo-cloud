using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetGuildMembersMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetGuildMembersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetGuildMembersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var page = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .GetMembersPageAsync(
                ctx.PlayerId,
                message.PageIndex,
                message.NameFilter,
                message.SearchType,
                ct
            )
            .ConfigureAwait(false);

        if (page is null)
            return;

        await ctx.SendComposerAsync(new GuildMembersMessageComposer { Page = page }, ct)
            .ConfigureAwait(false);
    }
}
