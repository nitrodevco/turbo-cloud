using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Users;

public class ScrGetUserInfoMessageHandler(IPlayerService playerService)
    : IMessageHandler<ScrGetUserInfoMessage>
{
    private readonly IPlayerService _playerService = playerService;

    public async ValueTask HandleAsync(
        ScrGetUserInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var player = _playerService.GetPlayerGrain(ctx.PlayerId);
        var snapshot = await player.GetSummaryAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ScrSendUserInfoMessageComposer
                {
                    ProductName = "club_habbo",
                    DaysToPeriodEnd = 0,
                    MemberPeriods = 0,
                    PeriodsSubscribedAhead = 0,
                    ResponseType = 0,
                    HasEverBeenMember = false,
                    IsVIP = false,
                    PastClubDays = 0,
                    PastVipDays = 0,
                    MinutesUntilExpiration = 0,
                    MinutesSinceLastModified = -1,
                },
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new AccountPreferencesEventMessageComposer
                {
                    UIVolume = 0,
                    FurniVolume = 0,
                    TraxVolume = 0,
                    FreeFlowChatDisabled = false,
                    RoomInvitesIgnored = false,
                    RoomCameraFollowDisabled = false,
                    UIFlags = UIFlags.FriendBarExpanded | UIFlags.RoomToolsExpanded,
                    PreferedChatStyle = 1,
                    WiredMenuButton = false,
                    WiredInspectButton = false,
                    PlayTestMode = false,
                    VariableSyntaxMode = 1,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
