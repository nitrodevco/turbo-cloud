using System.Threading;
using System.Threading.Tasks;
using Orleans;
using SuperSocket.Connection;
using Turbo.Authentication;
using Turbo.Contracts.Enums.Players;
using Turbo.Messages.Registry;
using Turbo.Players.Abstractions;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Session;

namespace Turbo.PacketHandlers.Handshake;

public class SSOTicketMessageHandler(AuthenticationService authService, IGrainFactory grainFactory)
    : IMessageHandler<SSOTicketMessage>
{
    private readonly AuthenticationService _authService = authService;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SSOTicketMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var ticket = message.SSO;
        var playerId = await _authService.GetPlayerIdFromTicketAsync(ticket).ConfigureAwait(false);

        if (playerId <= 0)
        {
            await ctx.Session.CloseAsync(CloseReason.Rejected).ConfigureAwait(false);

            return;
        }

        var endpoint = _grainFactory.GetGrain<IPlayerEndpointGrain>(playerId);

        await endpoint.BindConnectionAsync(ctx.Session.SessionID).ConfigureAwait(false);

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

        ctx.Session.SetPlayerId(playerId);

        await ctx
            .Session.SendComposerAsync(
                new AuthenticationOKMessage
                {
                    AccountId = playerId,
                    SuggestedLoginActions = [],
                    IdentityId = playerId,
                },
                ct
            )
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(
                new NavigatorSettingsMessageComposer { HomeRoomId = 0, RoomIdToEnter = 0 },
                ct
            )
            .ConfigureAwait(false);
        /* await ctx
            .Session.SendComposerAsync(
                new FavouritesMessage { Limit = 0, FavoriteRoomIds = [] },
                ct
            )
            .ConfigureAwait(false); */
        await ctx
            .Session.SendComposerAsync(
                new NoobnessLevelMessage { NoobnessLevel = NoobnessLevelEnum.NotNoob },
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
            .Session.SendComposerAsync(
                new AvailabilityStatusMessageComposer
                {
                    IsOpen = true,
                    OnShutDown = false,
                    IsAuthenticHabbo = true,
                },
                ct
            )
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(
                new IsFirstLoginOfDayMessage { IsFirstLoginOfDay = true },
                ct
            )
            .ConfigureAwait(false);
        /* await ctx
            .Session.SendComposerAsync(new FigureSetIdsMessage(), ct)
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(
                new NavigatorSettingsMessage { HomeRoomId = 0, RoomIdToEnter = 0 },
                ct
            )
            .ConfigureAwait(false); */
        await ctx
            .Session.SendComposerAsync(new RoomForwardMessageComposer { RoomId = 1 }, ct)
            .ConfigureAwait(false);
    }
}
