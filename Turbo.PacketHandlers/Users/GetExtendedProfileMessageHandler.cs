using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

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

        var snapshot = await _grainFactory
            .GetPlayerGrain(targetUserId)
            .GetExtendedProfileSnapshotAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ExtendedProfileMessageComposer
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
                    Guilds = snapshot.Guilds,
                    LastAccessSinceInSeconds = snapshot.LastAccessSinceInSeconds,
                    OpenProfileWindow = snapshot.OpenProfileWindow,
                    IsHidden = snapshot.IsHidden,
                    AccountLevel = snapshot.AccountLevel,
                    IntegerField24 = snapshot.IntegerField24,
                    StarGemCount = snapshot.StarGemCount,
                    BooleanField26 = snapshot.BooleanField26,
                    BooleanField27 = snapshot.BooleanField27,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
