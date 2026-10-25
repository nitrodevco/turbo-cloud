using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Events;
using Turbo.Messages.Registry;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Campaigns;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;
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
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Onboarding;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Handshake;

[AllowAnonymous]
public class SSOTicketMessageHandler(
    IAuthenticationService authService,
    ISessionGateway sessionGateway,
    IGrainFactory grainFactory,
    INavigatorService navigatorService,
    ISanctionService sanctionService,
    IHotelAvailability hotelAvailability,
    IHotelTextProvider textProvider,
    IPlayerClothingService clothing,
    ICallForHelpService callForHelp,
    IPlayerOnboardingService onboarding,
    IModerationToolService moderation,
    IModerationIssueService issues,
    IPlayerNoticeService notices,
    IClientTelemetry telemetry,
    IPlayPolicies playPolicies,
    ICampaignCalendarService campaignCalendars,
    EventSystem? events = null
) : IMessageHandler<SSOTicketMessage>
{
    /// <summary>The notification type of a play policy's refusal at login; a hotel styles it with <c>notification.turbo.play.refused</c>.</summary>
    public const string PLAY_REFUSED = "turbo.play.refused";

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
        // A connection logs in once; a second ticket on it is not a way to become someone else.
        if (ctx.PlayerId > 0)
            return;

        var ticket = message.SSO;
        var playerId = await _authService
            .GetPlayerIdFromTicketAsync(ticket, ct)
            .ConfigureAwait(false);

        if (playerId <= 0)
        {
            telemetry.RecordLoginStep(ctx.SessionKey, ctx.PlayerId, ClientLoginSteps.AUTH_FAIL);
            Failed(ctx, null, LoginFailure.InvalidTicket);
            await ctx.CloseSessionAsync("invalid SSO ticket").ConfigureAwait(false);

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
            telemetry.RecordLoginStep(ctx.SessionKey, playerId, ClientLoginSteps.AUTH_FAIL);
            Failed(ctx, playerId, LoginFailure.Banned);
            await ctx.CloseSessionAsync("banned").ConfigureAwait(false);

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
            telemetry.RecordLoginStep(ctx.SessionKey, playerId, ClientLoginSteps.AUTH_FAIL);
            Failed(ctx, playerId, LoginFailure.Maintenance);
            await ctx.CloseSessionAsync("hotel closed for maintenance").ConfigureAwait(false);

            return;
        }

        // The hotel's plugins may ask something of the player first; the site's Play asked the same.
        var play = await playPolicies
            .CheckAsync(new PlayRequest(playerId, PlaySource.Login, ctx.RemoteAddress), ct)
            .ConfigureAwait(false);

        if (!play.Allowed)
        {
            await ctx.SendComposerAsync(PlayRefusal(play), ct).ConfigureAwait(false);
            telemetry.RecordLoginStep(ctx.SessionKey, playerId, ClientLoginSteps.AUTH_FAIL);
            Failed(ctx, playerId, LoginFailure.RefusedByPolicy, play.Reason);
            await ctx.CloseSessionAsync("refused by a play policy").ConfigureAwait(false);

            return;
        }

        await _sessionGateway
            .AddSessionToPlayerAsync(ctx.SessionKey, playerId)
            .ConfigureAwait(false);
        telemetry.RecordLoginStep(ctx.SessionKey, playerId, ClientLoginSteps.AUTH_OK);
        events?.PublishCommitted(
            new PlayerLoggedInEvent { PlayerId = playerId, RemoteAddress = ctx.RemoteAddress }
        );

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
        // The effects the player owns, with what is left of any that is running.
        var effectsTask = _grainFactory.GetPlayerEffectGrain(playerId).GetEffectsAsync(ct);
        // The clothing they own, which the avatar editor offers them besides what everyone has.
        var ownedClothingTask = clothing.GetOwnedAsync(playerId, ct);
        // The clothing furni they have bound, which the client puts on without asking again.
        var boundClothingTask = clothing.GetBoundFurnitureNamesAsync(playerId, ct);
        // The new user flow's steps the player still has to do; the client draws the flow for them.
        var loginActionsTask = onboarding.GetLoginActionsAsync(playerId, ct);
        // Whether they are a new user, for the client's noob lobby offer and noob-only rooms.
        var noobnessTask = onboarding.GetNoobnessLevelAsync(playerId, ct);

        await Task.WhenAll(
                settingsTask,
                favouriteRoomIdsTask,
                clubGiftsTask,
                effectsTask,
                ownedClothingTask,
                boundClothingTask,
                loginActionsTask,
                noobnessTask
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new AuthenticationOKMessage
                {
                    AccountId = playerId,
                    SuggestedLoginActions = [.. await loginActionsTask.ConfigureAwait(false)],
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

        // Habbo pushes every activity point balance right after AuthenticationOK; GetCreditsInfo
        // is answered with the credits only.
        await ctx.SendComposerAsync(
                new ActivityPointsMessageComposer
                {
                    PointsByCategoryId = await _grainFactory
                        .GetPlayerWalletGrain(playerId)
                        .GetActivityPointsAsync(ct)
                        .ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);

        // What they wore last may not be theirs to wear now - club clothing after the club ran
        // out - so it is fitted again before anyone sees it.
        await _grainFactory.GetPlayerGrain(playerId).RefitFigureAsync(ct).ConfigureAwait(false);
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
                    BoundFurnitureNames = await boundClothingTask.ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new NoobnessLevelMessage
                {
                    NoobnessLevel = await noobnessTask.ConfigureAwait(false),
                },
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
        // What a call for help can be about: the help window builds its report flow from these.
        await ctx.SendComposerAsync(
                new CfhTopicsInitMessageComposer
                {
                    Categories = await callForHelp.GetTopicsAsync(ct).ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);

        // A moderator's tool opens on ModeratorInit (ModerationMessageHandler.onModeratorInit).
        // Its issue queue starts with the calls for help still open, and the issue handler opens
        // where the moderator last left it (IssueManager.setToolPreferences).
        if (await moderation.GetInitAsync(playerId, ct).ConfigureAwait(false) is { } init)
        {
            await ctx.SendComposerAsync(
                    new ModeratorInitMessageComposer
                    {
                        Init = init with
                        {
                            Issues = await issues.GetOpenIssuesAsync(ct).ConfigureAwait(false),
                        },
                    },
                    ct
                )
                .ConfigureAwait(false);

            if (
                await issues.GetPreferencesAsync(playerId, ct).ConfigureAwait(false) is
                { } preferences
            )
                await ctx.SendComposerAsync(
                        new ModeratorToolPreferencesEventMessageComposer
                        {
                            Preferences = preferences,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
        }

        // The reward tracks come unasked: the progression menu counts their claimable prizes.
        await _grainFactory
            .GetPlayerRewardTrackGrain(playerId)
            .SendTracksAsync(ct)
            .ConfigureAwait(false);
        // Then the Habbicons the player has and the Habbicon shop, also unasked: Habbo sends
        // UserHabbicons and HabbiconShopData right after RewardTracks.
        await _grainFactory
            .GetPlayerHabbiconGrain(playerId)
            .SendLoginAsync(ct)
            .ConfigureAwait(false);

        // The campaign calendar the client opens on openView/calendar: it never asks for it
        // (HabboCampaigns only listens for CampaignCalendarData), so it comes at login.
        if (
            await campaignCalendars.GetCalendarAsync(playerId, ct).ConfigureAwait(false) is
            { } calendar
        )
            await ctx.SendComposerAsync(calendar, ct).ConfigureAwait(false);
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
        // Official logins answer false, a new account's first one included; the client reads it only
        // to open the seasonal quest calendar, which the hotel has switched off.
        await ctx.SendComposerAsync(new IsFirstLoginOfDayMessage { IsFirstLoginOfDay = false }, ct)
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
        // Persistent notifications that came while they were away (a wired chest's), last, as
        // bubbles over a hotel view that is already up.
        await notices.DeliverPersistentAsync(playerId, ct).ConfigureAwait(false);
    }

    private void Failed(
        MessageContext ctx,
        PlayerId? playerId,
        LoginFailure failure,
        string? reason = null
    ) =>
        events?.PublishCommitted(
            new PlayerLoginFailedEvent
            {
                PlayerId = playerId,
                Failure = failure,
                Reason = reason,
                RemoteAddress = ctx.RemoteAddress,
            }
        );

    /// <summary>
    /// A play policy's refusal as Habbo's notification popup (<c>NotificationDialog</c>): its
    /// reason, and a button to the address it gives.
    /// </summary>
    private static NotificationDialogMessageComposer PlayRefusal(PlayDecision play)
    {
        var parameters = ImmutableDictionary.CreateBuilder<string, string>();

        parameters.Add("display", "POP_UP");
        parameters.Add("message", play.Reason ?? string.Empty);

        if (!string.IsNullOrEmpty(play.RedirectUrl))
            parameters.Add("linkUrl", play.RedirectUrl);

        return new NotificationDialogMessageComposer
        {
            NotificationType = PLAY_REFUSED,
            Parameters = parameters.ToImmutable(),
        };
    }
}
