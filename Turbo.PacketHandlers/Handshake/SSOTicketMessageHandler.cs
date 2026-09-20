using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Messages.Outgoing.Inventory.Clothing;
using Turbo.Primitives.Messages.Outgoing.Mysterybox;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Perk;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Handshake;

public class SSOTicketMessageHandler(
    IAuthenticationService authService,
    ISessionGateway sessionGateway,
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<SSOTicketMessage>
{
    private readonly IAuthenticationService _authService = authService;
    private readonly ISessionGateway _sessionGateway = sessionGateway;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        SSOTicketMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
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
        var settings = await _grainFactory
            .GetPlayerSettingsGrain(playerId)
            .GetSettingsAsync(ct)
            .ConfigureAwait(false);
        var favouriteRoomIds = await _navigatorService
            .GetFavouriteRoomIdsAsync(playerId, ct)
            .ConfigureAwait(false);

        // The client enters the home room on login.
        await ctx.SendComposerAsync(
                new NavigatorSettingsMessageComposer
                {
                    HomeRoomId = settings.HomeRoomId,
                    RoomIdToEnter = settings.HomeRoomId,
                },
                ct
            )
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new FavouritesMessageComposer
                {
                    Limit = _navigatorService.FavouriteRoomLimit,
                    FavoriteRoomIds = [.. favouriteRoomIds.Select(x => x.Value)],
                },
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
        // The subscription grain owns both the club level and the Builders Club countdown, and
        // sends them again itself whenever they change.
        await _grainFactory
            .GetPlayerSubscriptionGrain(playerId)
            .SendStatusAsync(ct)
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

        // Club gifts waiting to be collected. A hotel that offers none answers this from memory,
        // so it costs a login nothing; the client draws nothing for a count below one.
        var clubGifts = await _grainFactory
            .GetCatalogPurchaseGrain(playerId)
            .GetClubGiftInfoAsync(ct)
            .ConfigureAwait(false);

        if (clubGifts.GiftsAvailable > 0)
            await ctx.SendComposerAsync(
                    new ClubGiftNotificationEventMessageComposer
                    {
                        NumGifts = clubGifts.GiftsAvailable,
                    },
                    ct
                )
                .ConfigureAwait(false);
        await ctx.SendComposerAsync(new AchievementsScoreEventMessageComposer { Score = 0 }, ct)
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(new IsFirstLoginOfDayMessage { IsFirstLoginOfDay = true }, ct)
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
                new PerkAllowancesMessageComposer
                {
                    Perks =
                    [
                        new PerkAllowanceItem
                        {
                            Code = "NAVIGATOR_ROOM_THUMBNAIL_CAMERA",
                            ErrorMessage = string.Empty,
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "JUDGE_CHAT_REVIEWS",
                            ErrorMessage = "requirement.unfulfilled.helper_level_6",
                            IsAllowed = false,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "MOUSE_ZOOM",
                            ErrorMessage = string.Empty,
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "HABBO_CLUB_OFFER_BETA",
                            ErrorMessage = string.Empty,
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "TRADE",
                            ErrorMessage = "requirement.unfulfilled.citizenship_level_3",
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "CAMERA",
                            ErrorMessage = string.Empty,
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "NAVIGATOR_PHASE_TWO_2014",
                            ErrorMessage = string.Empty,
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "BUILDER_AT_WORK",
                            ErrorMessage = "requirement.unfulfilled.group_membership",
                            IsAllowed = false,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "CALL_ON_HELPERS",
                            ErrorMessage = string.Empty,
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "CITIZEN",
                            ErrorMessage = string.Empty,
                            IsAllowed = true,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "USE_GUIDE_TOOL",
                            ErrorMessage = "requirement.unfulfilled.helper_level_4",
                            IsAllowed = false,
                        },
                        new PerkAllowanceItem
                        {
                            Code = "VOTE_IN_COMPETITIONS",
                            ErrorMessage = "requirement.unfulfilled.helper_level_2",
                            IsAllowed = false,
                        },
                    ],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
