using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Handshake;

public class SSOTicketMessageHandler(
    IAuthenticationService authService,
    ISessionGateway sessionGateway
) : IMessageHandler<SSOTicketMessage>
{
    private readonly IAuthenticationService _authService = authService;
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    public async ValueTask HandleAsync(
        SSOTicketMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        try
        {
            var ticket = message.SSO;
            var playerId = await _authService
                .GetPlayerIdFromTicketAsync(ticket, ct)
                .ConfigureAwait(false);

            if (playerId <= 0)
            {
                await ctx.CloseSessionAsync().ConfigureAwait(false);

                return;
            }

            await _sessionGateway
                .AddSessionToPlayerAsync(ctx.SessionKey, playerId)
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(
                    new AuthenticationOKMessage
                    {
                        AccountId = playerId,
                        SuggestedLoginActions = [],
                        IdentityId = playerId,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new NavigatorSettingsMessageComposer { HomeRoomId = 1, RoomIdToEnter = 1 },
                    ct
                )
                .ConfigureAwait(false);
            /* await ctx
                .SendComposerAsync(
                    new FavouritesMessage { Limit = 0, FavoriteRoomIds = [] },
                    ct
                )
                .ConfigureAwait(false); */
            await ctx.SendComposerAsync(
                    new NoobnessLevelMessage { NoobnessLevel = NoobnessLevelType.NotNoob },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new UserRightsMessage
                    {
                        ClubLevel = ClubLevelType.Vip,
                        SecurityLevel = SecurityLevelType.Administrator,
                        IsAmbassador = false,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new AvailabilityStatusMessageComposer
                    {
                        IsOpen = true,
                        OnShutDown = false,
                        IsAuthenticHabbo = true,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new IsFirstLoginOfDayMessage { IsFirstLoginOfDay = true },
                    ct
                )
                .ConfigureAwait(false);
            /* await ctx
                .SendComposerAsync(new FigureSetIdsMessage(), ct)
                .ConfigureAwait(false); */
        }
        catch (Exception) { }

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
    }
}
