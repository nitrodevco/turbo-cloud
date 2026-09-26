using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Players.Grains.Navigator;
using Turbo.Primitives.Players.Grains.Settings;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Grains.Wardrobe;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;

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

    public static IPlayerPresenceGrain GetPlayerPresenceGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerPresenceGrain>(playerId.Value);

    public static IPlayerDirectoryGrain GetPlayerDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IPlayerDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IBadgeDirectoryGrain GetBadgeDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IBadgeDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IGuildDirectoryGrain GetGuildDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IGuildDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IGuildGrain GetGuildGrain(this IGrainFactory factory, GuildId guildId) =>
        factory.GetGrain<IGuildGrain>((long)guildId.Value);

    public static IPlayerGuildGrain GetPlayerGuildGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerGuildGrain>(playerId.Value);

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
    /// Gives back what a charge took, after the charge went through but the thing it paid for
    /// could not be made. A debit never shares a transaction with what it buys — the wallet is
    /// another grain — so every "charge, then create" path ends here on failure. A refund that
    /// itself fails is logged and nothing more: there is no third place to put the money, and
    /// throwing would lose the original failure.
    /// </summary>
    public static async Task RefundAsync(
        this IGrainFactory factory,
        PlayerId playerId,
        IEnumerable<WalletDebit> debits,
        ILogger logger,
        string what
    )
    {
        var wallet = factory.GetPlayerWalletGrain(playerId);

        foreach (var debit in debits)
        {
            try
            {
                // Not the caller's token: a refund that has started must not be abandoned.
                await wallet
                    .CreditAsync(debit.CurrencyKind, debit.Amount, CancellationToken.None)
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
