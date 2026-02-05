using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Users;

public class GetExtendedProfileMessageHandler : IMessageHandler<GetExtendedProfileMessage>
{
    private readonly IGrainFactory _grainFactory;

    public GetExtendedProfileMessageHandler(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async ValueTask HandleAsync(
        GetExtendedProfileMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var targetUserId = message.UserId;
        if (targetUserId <= 0)
            return;

        try
        {
            // Get player data from the grain
            var grain = _grainFactory.GetGrain<IPlayerGrain>(targetUserId);
            var snapshot = await grain.GetExtendedProfileSnapshotAsync(ct).ConfigureAwait(false);

            var response = new ExtendedProfileMessageComposer
            {
                UserId = (int)snapshot.UserId,
                UserName = snapshot.UserName,
                Figure = snapshot.Figure,
                Motto = snapshot.Motto,
                CreationDate = snapshot.CreationDate,
                AchievementScore = snapshot.AchievementScore,
                FriendCount = snapshot.FriendCount,
                IsFriend = snapshot.IsFriend,
                IsFriendRequestSent = snapshot.IsFriendRequestSent,
                IsOnline = snapshot.IsOnline,
                Guilds = new List<GuildInfo>(), // TODO: Convert snapshot.Guilds when guild system is ready
                LastAccessSinceInSeconds = snapshot.LastAccessSinceInSeconds,
                OpenProfileWindow = snapshot.OpenProfileWindow,
                IsHidden = snapshot.IsHidden,
                AccountLevel = snapshot.AccountLevel,
                IntegerField24 = snapshot.IntegerField24,
                StarGemCount = snapshot.StarGemCount,
                BooleanField26 = snapshot.BooleanField26,
                BooleanField27 = snapshot.BooleanField27
            };

            await ctx.SendComposerAsync(response, ct).ConfigureAwait(false);
        }
        catch
        {
            // TODO: Log error
        }
    }
}
