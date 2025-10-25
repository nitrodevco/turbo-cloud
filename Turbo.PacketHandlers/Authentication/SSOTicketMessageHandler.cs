using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Connection;
using Turbo.Authentication;
using Turbo.Contracts.Enums.Players;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.PacketHandlers.Authentication;

public class SSOTicketMessageHandler(AuthenticationService authService)
    : IMessageHandler<SSOTicketMessage>
{
    private readonly AuthenticationService _authService = authService;

    public async ValueTask HandleAsync(
        SSOTicketMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var ticket = message.SSO;
        var result = await _authService.GetPlayerIdFromTicketAsync(ticket).ConfigureAwait(false);

        // auth ok
        // effects
        // nav settings
        // favorites
        // unseen items
        // figure set ids
        // noobness level
        // user rights
        // availability status
        // infofeed enable
        // activity points
        // achievements score
        // is first login of day
        // mystery box keys
        // builders club status
        // cfh topics
        //

        if (result > 0)
        {
            ctx.Session.SetPlayerId(result);

            await ctx
                .Session.SendComposerAsync(
                    new AuthenticationOKMessage
                    {
                        AccountId = (int)ctx.Session.PlayerId,
                        SuggestedLoginActions = [],
                        IdentityId = (int)ctx.Session.PlayerId,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx
                .Session.SendComposerAsync(
                    new UserRightsMessage
                    {
                        ClubLevel = ClubLevelEnum.Vip,
                        SecurityLevel = SecurityLevelEnum.Administrator,
                        IsAmbassador = false,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx
                .Session.SendComposerAsync(new NoobnessLevelMessage(), ct)
                .ConfigureAwait(false);
            /* await ctx
                .Session.SendComposerAsync(new FigureSetIdsMessage(), ct)
                .ConfigureAwait(false);
            await ctx
                .Session.SendComposerAsync(
                    new NavigatorSettingsMessage { HomeRoomId = 0, RoomIdToEnter = 0 },
                    ct
                )
                .ConfigureAwait(false);
            await ctx
                .Session.SendComposerAsync(
                    new AvailabilityStatusMessage
                    {
                        IsOpen = true,
                        OnShutDown = false,
                        IsAuthenticHabbo = true,
                    },
                    ct
                )
                .ConfigureAwait(false); */
        }
        else
        {
            await ctx.Session.CloseAsync(CloseReason.Rejected).ConfigureAwait(false);

            return;
        }
    }
}
