using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// How much furni a member has standing in the homeroom. The client asks before it kicks or
/// leaves and waits for the answer, so a silent handler stops the kick happening at all.
///
/// The count comes from the room rather than from the guild: the room holds the live truth, and
/// asking it here rather than from the group grain is what keeps the group grain off the room.
/// </summary>
public class GetMemberGuildItemCountMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetMemberGuildItemCountMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetMemberGuildItemCountMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0 || message.PlayerId <= 0)
            return;

        var guild = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetSummaryAsync(message.GuildId, ct)
            .ConfigureAwait(false);

        if (guild is null)
            return;

        var count = await _grainFactory
            .GetRoomGrain(guild.RoomId)
            .GetItemCountByOwnerAsync(message.PlayerId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new GuildMemberFurniCountInHQMessageComposer
                {
                    PlayerId = message.PlayerId,
                    FurniCount = count,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
