using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Messages.Outgoing.Inventory.Clothing;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Messages.Outgoing.Mysterybox;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.PacketHandlers.Handshake;

public class SSOTicketMessageHandler(
    IAuthenticationService authService,
    ISessionGateway sessionGateway,
    IGrainFactory grainFactory,
    INavigatorService navigatorService,
    ISanctionService sanctionService,
    IHotelAvailability hotelAvailability,
    IHotelTextProvider textProvider,
    IPlayerClothingService clothing
) : IMessageHandler<SSOTicketMessage>
{
    private readonly IAuthenticationService _authService = authService;
    private readonly ISessionGateway _sessionGateway = sessionGateway;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;
    private readonly ISanctionService _sanctionService = sanctionService;
    private readonly IHotelAvailability _hotelAvailability = hotelAvailability;
    private readonly IHotelTextProvider _textProvider = textProvider;

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

        // A banned player is told why and turned away before the session knows who they are.
        if (await _sanctionService.GetActiveBanAsync(playerId, ct).ConfigureAwait(false) is { } ban)
        {
            await ctx.SendComposerAsync(
                    new UserBannedMessageComposer
                    {
                        Message = await SanctionMessages
                            .BanMessageAsync(ban, _textProvider, ct)
                            .ConfigureAwait(false),
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.CloseSessionAsync().ConfigureAwait(false);

            return;
        }

        // In maintenance only staff who may stay get in.
        if (!await _hotelAvailability.AdmitsAsync(playerId, ct).ConfigureAwait(false))
        {
            await ctx.SendComposerAsync(
                    new HabboBroadcastMessageComposer
                    {
                        Message = await AvailabilityMessages
                            .MaintenanceStartedAsync(_textProvider, ct)
                            .ConfigureAwait(false),
                    },
                    ct
                )
                .ConfigureAwait(false);
            await ctx.CloseSessionAsync().ConfigureAwait(false);

            return;
        }

        await _sessionGateway
            .AddSessionToPlayerAsync(ctx.SessionKey, playerId)
            .ConfigureAwait(false);

        // Five reads from five different grains, none depending on another, so they are asked
        // side by side and awaited where their answers are sent. The composers still go out in
        // the order below.
        var settingsTask = _grainFactory.GetPlayerSettingsGrain(playerId).GetSettingsAsync(ct);
        var favouriteRoomIdsTask = _navigatorService.GetFavouriteRoomIdsAsync(playerId, ct);
        // Club gifts waiting to be collected. A hotel that offers none answers this from memory,
        // so it costs a login nothing; the client draws nothing for a count below one.
        var clubGiftsTask = _grainFactory
            .GetCatalogPurchaseGrain(playerId)
            .GetClubGiftInfoAsync(ct);
        // The welcome message staff set in the admin panel; answered from memory.
        var welcomeMessageTask = _grainFactory.GetWelcomeMessageGrain().GetMessageAsync(ct);
        // The effects the player owns, with what is left of any that is running.
        var effectsTask = _grainFactory.GetPlayerEffectGrain(playerId).GetEffectsAsync(ct);
        // The clothing they own, which the avatar editor offers them besides what everyone has.
        var ownedClothingTask = clothing.GetOwnedAsync(playerId, ct);

        await Task.WhenAll(
                settingsTask,
                favouriteRoomIdsTask,
                clubGiftsTask,
                welcomeMessageTask,
                effectsTask,
                ownedClothingTask
            )
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
        // Establish the current restriction baseline before any permission-dependent login reads.
        await _grainFactory
            .GetPlayerPermissionGrain(playerId)
            .NotifyActiveRestrictionsAsync(ct)
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new AvatarEffectsMessageComposer
                {
                    Effects = await effectsTask.ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);

        // What they wore last may not be theirs to wear now - club clothing after the club ran
        // out - so it is fitted again before anyone sees it.
        await _grainFactory.GetPlayerGrain(playerId).RefitFigureAsync(ct).ConfigureAwait(false);
        await ctx.SendComposerAsync(new AvatarEffectsMessageComposer { Effects = [] }, ct)
            .ConfigureAwait(false);
        var settings = await settingsTask.ConfigureAwait(false);
        var favouriteRoomIds = await favouriteRoomIdsTask.ConfigureAwait(false);

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
                    Limit = await _grainFactory
                        .GetLimitAsync(
                            playerId,
                            PermissionMetaKeys.Limit.FAVOURITE_ROOMS,
                            _navigatorService.FavouriteRoomLimit,
                            ct
                        )
                        .ConfigureAwait(false),
                    FavoriteRoomIds = [.. favouriteRoomIds.Select(x => x.Value)],
                },
                ct
            )
            .ConfigureAwait(false);
        // What arrived since they last looked, including while they were offline.
        var unseenItems = await _grainFactory
            .GetPlayerUnseenItemsGrain(playerId)
            .GetUnseenItemsAsync(ct)
            .ConfigureAwait(false);

        if (!unseenItems.IsEmpty)
            await ctx.SendComposerAsync(
                    new UnseenItemsEventMessageComposer { Items = unseenItems },
                    ct
                )
                .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new FigureSetIdsEventMessageComposer
                {
                    FigureSetIds = [.. (await ownedClothingTask.ConfigureAwait(false)).Order()],
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
        // Security level, ambassador flag, club level and perks, all worked out from the player's
        // permissions; the permission grain sends them again itself whenever they change.
        await _grainFactory
            .GetPlayerPermissionGrain(playerId)
            .SendClientStateAsync(ct)
            .ConfigureAwait(false);
        // The Builders Club countdown, which the subscription grain resends when it changes.
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

        await _grainFactory
            .GetPlayerWalletGrain(playerId)
            .DeliverPendingRewardsAsync(ct)
            .ConfigureAwait(false);

        var clubGifts = await clubGiftsTask.ConfigureAwait(false);

        if (clubGifts.GiftsAvailable > 0)
            await ctx.SendComposerAsync(
                    new ClubGiftNotificationEventMessageComposer
                    {
                        NumGifts = clubGifts.GiftsAvailable,
                    },
                    ct
                )
                .ConfigureAwait(false);
        await global::Turbo
            .Primitives.Achievements.Orleans.AchievementGrainExtensions.GetPlayerAchievementGrain(
                _grainFactory,
                playerId
            )
            .ReconcileAsync(ct)
            .ConfigureAwait(false);
        var achievementSummary = await _grainFactory
            .GetPlayerGrain(playerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new AchievementsScoreEventMessageComposer
                {
                    Score = achievementSummary.AchievementScore,
                },
                ct
            )
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

        // Last, so it opens over the hotel view. None is shown while the message is empty.
        var welcomeMessage = await welcomeMessageTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(welcomeMessage))
            await ctx.SendComposerAsync(
                    new MOTDNotificationEventMessageComposer { Messages = [welcomeMessage] },
                    ct
                )
                .ConfigureAwait(false);
    }
}
