using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Messages.Outgoing.Inventory.Clothing;
using Turbo.Primitives.Messages.Outgoing.Mysterybox;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Notifications;
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
            await ctx.SendComposerAsync(new AvatarEffectsMessageComposer { Effects = [] }, ct)
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new NavigatorSettingsMessageComposer { HomeRoomId = 1, RoomIdToEnter = 0 },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new FavouritesMessageComposer { Limit = 0, FavoriteRoomIds = [] },
                    ct
                )
                .ConfigureAwait(false);
            // unseen items
            await ctx.SendComposerAsync(
                    new FigureSetIdsEventMessageComposer
                    {
                        FigureSetIds = [],
                        BoundFurnitureNames = [],
                    },
                    ct
                )
                .ConfigureAwait(false);
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
            await ctx.SendComposerAsync(new InfoFeedEnableMessageComposer { Enabled = true }, ct)
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new ActivityPointsMessageComposer
                    {
                        PointsByCategoryId = ImmutableDictionary<int, int>.Empty,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(new AchievementsScoreEventMessageComposer { Score = 0 }, ct)
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new IsFirstLoginOfDayMessage { IsFirstLoginOfDay = true },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new MysteryBoxKeysMessageComposer
                    {
                        BoxColor = string.Empty,
                        KeyColor = string.Empty,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.SendComposerAsync(
                    new BuildersClubSubscriptionStatusMessageComposer
                    {
                        SecondsLeft = 0,
                        FurniLimit = 0,
                        MaxFurniLimit = 0,
                        SecondsLeftWithGrace = 0,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}
