using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

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

        var directoryGrain = _grainFactory.GetPlayerDirectoryGrain();
        var playerId = await directoryGrain.GetPlayerIdAsync(message.UserName, ct).ConfigureAwait(false);
        if (playerId is null)
            return;
        var snapshot = await _grainFactory
            .GetPlayerGrain(playerId.Value)
            .GetExtendedProfileSnapshotAsync(ct)
            .ConfigureAwait(false);

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
            Guilds = snapshot
                .Guilds.Select(x => new GuildInfo
                {
                    GroupId = x.GroupId,
                    GroupName = x.GroupName,
                    BadgeCode = x.BadgeCode,
                    PrimaryColor = x.PrimaryColor,
                    SecondaryColor = x.SecondaryColor,
                    Favourite = x.Favourite,
                    OwnerId = x.OwnerId,
                    HasForum = x.HasForum
                })
                .ToList(),
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
}
