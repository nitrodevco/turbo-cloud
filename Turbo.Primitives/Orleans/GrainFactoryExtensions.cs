using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Games.Grains;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Habbicons.Grains;
using Turbo.Primitives.Hotel.Grains;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Grains;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Players.Grains.Navigator;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Grains.Settings;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Grains.Wardrobe;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Polls;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Primitives.Scheduling.Grains;
using Turbo.Primitives.Sound.Grains;
using Turbo.Primitives.WiredTrading.Grains;

namespace Turbo.Primitives.Orleans;

public static class GrainFactoryExtensions
{
    public static IRoomGrain GetRoomGrain(this IGrainFactory factory, RoomId roomId) =>
        factory.GetGrain<IRoomGrain>((long)roomId.Value);

    public static IRoomPersistenceGrain GetRoomPersistenceGrain(
        this IGrainFactory factory,
        RoomId roomId
    ) => factory.GetGrain<IRoomPersistenceGrain>((long)roomId.Value);

    public static IRoomTradeGrain GetRoomTradeGrain(this IGrainFactory factory, RoomId roomId) =>
        factory.GetGrain<IRoomTradeGrain>((long)roomId.Value);

    public static IWiredChestGrain GetWiredChestGrain(
        this IGrainFactory factory,
        RoomObjectId chestId
    ) => factory.GetGrain<IWiredChestGrain>((long)chestId.Value);

    public static IJukeboxGrain GetJukeboxGrain(
        this IGrainFactory factory,
        RoomObjectId jukeboxId
    ) => factory.GetGrain<IJukeboxGrain>((long)jukeboxId.Value);

    public static ISongDirectoryGrain GetSongDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<ISongDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IWiredTransactionLogGrain GetWiredTransactionLogGrain(
        this IGrainFactory factory,
        RoomId roomId
    ) => factory.GetGrain<IWiredTransactionLogGrain>((long)roomId.Value);

    public static IWiredTradeGrain GetWiredTradeGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IWiredTradeGrain>(playerId.Value);

    public static IWiredLinkOwnerGrain GetWiredLinkOwnerGrain(
        this IGrainFactory factory,
        PlayerId ownerId
    ) => factory.GetGrain<IWiredLinkOwnerGrain>(ownerId.Value);

    public static IRoomDirectoryGrain GetRoomDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IRoomDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IPlayerGrain GetPlayerGrain(this IGrainFactory factory, PlayerId playerId) =>
        factory.GetGrain<IPlayerGrain>((long)playerId.Value);

    /// <summary>
    /// Sends a composer to a player, wherever they are connected. This is the one way to reach
    /// a player from a grain, module, logic class or service; do not spell out
    /// <c>GetPlayerPresenceGrain(id).SendComposerAsync(...)</c> or wrap it in a local helper.
    /// </summary>
    public static Task SendComposerToPlayerAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        IComposer composer,
        CancellationToken ct
    ) => factory.GetPlayerPresenceGrain(playerId).SendComposerAsync(composer, ct);

    /// <summary>Accepts a composer only for a currently attached player session, without an offline backlog.</summary>
    public static Task<bool> TrySendComposerToPlayerAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        IComposer composer,
        CancellationToken ct
    ) => factory.GetPlayerPresenceGrain(playerId).TrySendComposerAsync(composer, ct);

    /// <summary>
    /// Whether the player's session <paramref name="sessionKey"/> accepted the extension
    /// <paramref name="capability"/>: what an extension packet's handler checks first, with the
    /// <c>PlayerId</c> and <c>SessionKey</c> of its message context (<c>docs/plugin-packets.md</c>).
    /// </summary>
    public static async Task<bool> SessionAcceptsCapabilityAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        SessionKey sessionKey,
        string capability,
        CancellationToken ct
    ) =>
        await factory
            .GetPlayerPresenceGrain(playerId)
            .GetClientCapabilityVersionAsync(sessionKey, capability, ct)
            .ConfigureAwait(false) > 0;

    /// <summary>The same composer to several players. Each presence is its own grain, so the sends run side by side.</summary>
    public static Task SendComposerToPlayersAsync(
        this IGrainFactory factory,
        IEnumerable<PlayerId> playerIds,
        IComposer composer,
        CancellationToken ct
    ) =>
        Task.WhenAll(
            playerIds.Select(playerId => factory.SendComposerToPlayerAsync(playerId, composer, ct))
        );

    /// <summary>
    /// Sends a plugin message to a player, wherever they are connected: written by its schema here
    /// and sent as <see cref="SendComposerToPlayerAsync"/> sends a composer, to a session that
    /// accepted <c>client.messages</c> (<c>docs/plugin-messages.md</c>).
    /// </summary>
    public static Task SendPluginMessageToPlayerAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        IPluginMessage message,
        CancellationToken ct
    ) => factory.SendComposerToPlayerAsync(playerId, PluginMessageComposer.Compose(message), ct);

    /// <summary>The same plugin message to several players, written once.</summary>
    public static Task SendPluginMessageToPlayersAsync(
        this IGrainFactory factory,
        IEnumerable<PlayerId> playerIds,
        IPluginMessage message,
        CancellationToken ct
    ) => factory.SendComposerToPlayersAsync(playerIds, PluginMessageComposer.Compose(message), ct);

    public static IPlayerPresenceGrain GetPlayerPresenceGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerPresenceGrain>(playerId.Value);

    public static IAdminAuthGrain GetAdminAuthGrain(this IGrainFactory factory) =>
        factory.GetGrain<IAdminAuthGrain>(SingletonGrainId.GLOBAL);

    public static IAdminAccountGrain GetAdminAccountGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IAdminAccountGrain>(playerId.Value);

    public static IPlayerDirectoryGrain GetPlayerDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IPlayerDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IPermissionGroupDirectoryGrain GetPermissionGroupDirectoryGrain(
        this IGrainFactory factory
    ) => factory.GetGrain<IPermissionGroupDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IPlayerPermissionGrain GetPlayerPermissionGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerPermissionGrain>(playerId.Value);

    /// <summary>
    /// Whether a player holds a permission node: the one check every gate makes. Answered from
    /// the player's permission grain, in memory. Pass a constant from <c>PermissionNodes</c>.
    /// </summary>
    public static Task<bool> HasPermissionAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        string node,
        CancellationToken ct
    ) => factory.GetPlayerPermissionGrain(playerId).HasAsync(node, ct);

    /// <summary>
    /// Which of <paramref name="playerIds"/> hold <paramref name="node"/>, asked of their
    /// permission grains at once: asked one after another, every online player cost a grain
    /// call's time before the answer.
    /// </summary>
    public static async Task<List<PlayerId>> WhoHoldsAsync(
        this IGrainFactory factory,
        IEnumerable<PlayerId> playerIds,
        string node,
        CancellationToken ct
    )
    {
        var players = playerIds.ToList();
        var held = await Task.WhenAll(players.Select(x => factory.HasPermissionAsync(x, node, ct)))
            .ConfigureAwait(false);

        return [.. players.Where((_, i) => held[i])];
    }

    /// <summary>
    /// A limit for one player: the meta key's value when their groups or they set one, otherwise
    /// <paramref name="fallback"/>, the hotel's configured default. The grain that enforces the
    /// limit passes its own config option.
    /// </summary>
    public static async Task<int> GetLimitAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        string key,
        int fallback,
        CancellationToken ct
    ) =>
        PermissionMeta.ReadLimit(
            await factory
                .GetPlayerPermissionGrain(playerId)
                .GetMetaAsync(key, ct)
                .ConfigureAwait(false),
            fallback
        );

    public static IBadgeDirectoryGrain GetBadgeDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IBadgeDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IGuildDirectoryGrain GetGuildDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IGuildDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IGuildGrain GetGuildGrain(this IGrainFactory factory, GuildId guildId) =>
        factory.GetGrain<IGuildGrain>((long)guildId.Value);

    public static IGuildForumGrain GetGuildForumGrain(
        this IGrainFactory factory,
        GuildId guildId
    ) => factory.GetGrain<IGuildForumGrain>(guildId.Value);

    public static IPlayerGuildForumGrain GetPlayerGuildForumGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerGuildForumGrain>(playerId.Value);

    public static IPlayerGuildGrain GetPlayerGuildGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerGuildGrain>(playerId.Value);

    public static IPlayerUnseenItemsGrain GetPlayerUnseenItemsGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerUnseenItemsGrain>(playerId.Value);

    public static IPlayerBadgeGrain GetPlayerBadgeGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerBadgeGrain>(playerId.Value);

    public static IPlayerEffectGrain GetPlayerEffectGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerEffectGrain>(playerId.Value);

    public static IGameDirectoryGrain GetGameDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IGameDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IBadgeLeaderboardGrain GetBadgeLeaderboardGrain(this IGrainFactory factory) =>
        factory.GetGrain<IBadgeLeaderboardGrain>(SingletonGrainId.GLOBAL);

    public static IPlayerWalletGrain GetPlayerWalletGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerWalletGrain>(playerId.Value);

    public static IInventoryGrain GetInventoryGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IInventoryGrain>(playerId.Value);

    public static ICatalogPurchaseGrain GetCatalogPurchaseGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<ICatalogPurchaseGrain>(playerId.Value);

    public static IBuildersClubGrain GetBuildersClubGrain(this IGrainFactory factory) =>
        factory.GetGrain<IBuildersClubGrain>(SingletonGrainId.GLOBAL);

    public static IWelcomeMessageGrain GetWelcomeMessageGrain(this IGrainFactory factory) =>
        factory.GetGrain<IWelcomeMessageGrain>(SingletonGrainId.GLOBAL);

    public static IAssetMovementDispatcherGrain GetAssetMovementDispatcherGrain(
        this IGrainFactory factory
    ) => factory.GetGrain<IAssetMovementDispatcherGrain>(SingletonGrainId.GLOBAL);

    public static IScheduledTaskGrain GetScheduledTaskGrain(this IGrainFactory factory) =>
        factory.GetGrain<IScheduledTaskGrain>(SingletonGrainId.GLOBAL);

    public static ICatalogLtdRaffleGrain GetLtdRaffleGrain(
        this IGrainFactory factory,
        int ltdSeriesId
    ) => factory.GetGrain<ICatalogLtdRaffleGrain>(ltdSeriesId);

    public static IPlayerMessengerGrain GetPlayerMessengerGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerMessengerGrain>(playerId.Value);

    public static IPlayerWardrobeGrain GetPlayerWardrobeGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerWardrobeGrain>(playerId.Value);

    public static IPlayerDailyTaskGrain GetPlayerDailyTaskGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerDailyTaskGrain>(playerId.Value);

    public static IPlayerRewardTrackGrain GetPlayerRewardTrackGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerRewardTrackGrain>(playerId.Value);

    public static IPlayerTargetedOfferGrain GetPlayerTargetedOfferGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerTargetedOfferGrain>(playerId.Value);

    public static IPlayerHabbiconGrain GetPlayerHabbiconGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerHabbiconGrain>(playerId.Value);

    public static IPlayerProductClaimGrain GetPlayerProductClaimGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerProductClaimGrain>(playerId.Value);

    public static IPlayerNuxGiftGrain GetPlayerNuxGiftGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerNuxGiftGrain>(playerId.Value);

    public static IPlayerRoomPollGrain GetPlayerRoomPollGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerRoomPollGrain>(playerId.Value);

    public static IPlayerQuestGrain GetPlayerQuestGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerQuestGrain>(playerId.Value);

    public static IPlayerSettingsGrain GetPlayerSettingsGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerSettingsGrain>(playerId.Value);

    public static IPlayerSubscriptionGrain GetPlayerSubscriptionGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerSubscriptionGrain>(playerId.Value);

    /// <summary>
    /// Whether this player holds a Habbo Club membership right now. It is here rather than spelled
    /// out at each call site because which subscription counts as "club" is one decision, and it
    /// was being made in two places.
    /// </summary>
    public static Task<bool> HasActiveClubAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        CancellationToken ct
    ) =>
        factory.GetPlayerSubscriptionGrain(playerId).HasActiveAsync(SubscriptionType.HabboClub, ct);

    /// <summary>
    /// Sends a player to a room. The only way to forward a player, from a handler, a grain or a
    /// wired box: never send <c>RoomForwardMessageComposer</c> yourself. The presence records
    /// how they arrive (<paramref name="entry"/>: a teleporter's far half, a room network, or a
    /// plain entry) and queues the forward in one call.
    /// </summary>
    public static Task ForwardPlayerToRoomAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        RoomId roomId,
        RoomEntrySnapshot entry,
        CancellationToken ct
    ) => factory.GetPlayerPresenceGrain(playerId).ForwardToRoomAsync(roomId, entry, ct);

    /// <summary>
    /// Whether the player is on their way into <paramref name="roomId"/> a way that passes the
    /// room's door (doorbell, password): through a teleporter, the way Habbo's does, or through a
    /// plugin furni's linked pair (<see cref="RoomEntrySnapshot.PassesDoor"/>). Bans and capacity
    /// still apply.
    /// </summary>
    public static async Task<bool> PassesRoomDoorAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    ) =>
        (
            await factory
                .GetPlayerPresenceGrain(playerId)
                .GetPendingRoomEntryAsync(roomId, ct)
                .ConfigureAwait(false)
        ).PassesDoor;

    /// <summary>A plain forward: the player arrives the ordinary way.</summary>
    public static Task ForwardPlayerToRoomAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    ) => factory.ForwardPlayerToRoomAsync(playerId, roomId, RoomEntrySnapshot.Default, ct);

    /// <summary>
    /// Gives back what a charge took, after the charge went through but the thing it paid for
    /// could not be made. A debit never shares a transaction with what it buys — the wallet is
    /// another grain — so every "charge, then create" path ends here on failure. A refund that
    /// itself fails is logged and nothing more: there is no third place to put the money, and
    /// throwing would lose the original failure. <paramref name="cause"/> is the charge's own: the
    /// refund is recorded under its cause, as an operation of its own that names the charge's.
    /// </summary>
    public static async Task RefundAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        IEnumerable<WalletDebit> debits,
        MovementCause cause,
        ILogger logger,
        string what
    )
    {
        var wallet = factory.GetPlayerWalletGrain(playerId);
        var refund = cause.ForRefund();

        // The i-th currency at the charge's i-th seq: each is a credit of its own, and a refund
        // given again for the same charge finds every one of them already paid.
        foreach (var (debit, seq) in debits.Select((debit, i) => (debit, cause.FirstSeq + i)))
        {
            try
            {
                // Not the caller's token: a refund that has started must not be abandoned.
                await wallet
                    .CreditAsync(
                        debit.CurrencyKind,
                        debit.Amount,
                        refund.StartingAt(seq),
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to refund {Amount} {CurrencyType} to player {PlayerId} for {What}",
                    debit.Amount,
                    debit.CurrencyKind.CurrencyType,
                    playerId.Value,
                    what
                );
            }
        }
    }

    public static IPlayerNavigatorGrain GetPlayerNavigatorGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerNavigatorGrain>(playerId.Value);
}
