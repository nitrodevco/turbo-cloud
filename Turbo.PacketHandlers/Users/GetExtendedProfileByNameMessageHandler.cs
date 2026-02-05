using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;

namespace Turbo.PacketHandlers.Users;

public class GetExtendedProfileByNameMessageHandler
    : IMessageHandler<GetExtendedProfileByNameMessage>
{
    private readonly IGrainFactory _grainFactory;

    public GetExtendedProfileByNameMessageHandler(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async ValueTask HandleAsync(
        GetExtendedProfileByNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(message.UserName))
            return;

        try
        {
            // Resolve username to player ID using the directory grain
            var directoryGrain = _grainFactory.GetGrain<IPlayerDirectoryGrain>(
                "default"  // PlayerDirectoryGrain uses a static key
            );
            var playerId = await directoryGrain.GetPlayerIdAsync(message.UserName, ct).ConfigureAwait(false);

            if (playerId is null or <= 0)
                return;

            // Get player data from the grain
            var grain = _grainFactory.GetGrain<IPlayerGrain>(playerId.Value);
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

