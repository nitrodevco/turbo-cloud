using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetGuildEditInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetGuildEditInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetGuildEditInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var guildGrain = _grainFactory.GetGuildGrain(message.GuildId);

        var guild = await guildGrain.GetSnapshotAsync(ct).ConfigureAwait(false);

        if (guild is null || guild.OwnerId != ctx.PlayerId)
            return;

        // The rooms are the player's and the rest is the group's, so they are read side by side
        // rather than by making the group grain ask for them.
        var roomsTask = _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .GetRoomOptionsAsync(message.GuildId, ct);
        var badgeTask = guildGrain.GetBadgePartsAsync(ct);
        var memberCountTask = guildGrain.GetMemberCountAsync(ct);

        await Task.WhenAll(roomsTask, badgeTask, memberCountTask).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new GuildEditInfoMessageComposer
                {
                    Guild = guild,
                    OwnedRooms = await roomsTask.ConfigureAwait(false),
                    IsOwner = true,
                    BadgeParts = await badgeTask.ConfigureAwait(false),
                    MemberCount = await memberCountTask.ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
