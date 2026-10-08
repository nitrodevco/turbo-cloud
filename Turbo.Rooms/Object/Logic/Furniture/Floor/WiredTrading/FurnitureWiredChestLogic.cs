using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

/// <summary>
/// A wired chest: furni that holds furni or credits. Its contents are its grain's
/// (<see cref="IWiredChestGrain"/>); this logic keeps the settings in map stuff data under the
/// keys the client reads (<see cref="WiredChestData"/>), decides who may do what, and mirrors
/// the contents' counts and preview into the stuff data and into <see cref="Summary"/>, which
/// wired conditions read without waiting.
/// <para>
/// Who may do what follows the client's rules (<c>wiredchests.lock_info.*</c>): the chest's
/// owner may always; on a wired chest, players with wired modification rights may too while it
/// is unlocked; anyone may donate when the owner allows it. Both owners may lock, only the
/// chest's owner unlocks, and a chest is locked whenever it is placed.
/// </para>
/// </summary>
public abstract class FurnitureWiredChestLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private int _viewerCount;

    protected override StuffDataType _stuffDataType => StuffDataType.MapKey;

    public abstract WiredChestKind Kind { get; }

    /// <summary>The contents as last told by the chest's grain.</summary>
    public WiredChestSummarySnapshot Summary { get; private set; } =
        WiredChestSummarySnapshot.Empty;

    public bool IsLocked => Flag(WiredChestData.LOCKED);

    public bool IsWiredEnabled => Flag(WiredChestData.IS_WIRED_ENABLED);

    /// <summary>The owner's "everyone can open the chest" (<c>~chest.is_open</c>).</summary>
    public bool EveryoneCanOpen => Flag(WiredChestData.EVERYONE_CAN_OPEN);

    /// <summary>The owner's "everyone can donate to the chest" (<c>~chest.is_donatable</c>).</summary>
    public bool EveryoneCanDonate => Flag(WiredChestData.EVERYONE_CAN_DONATE);

    /// <summary>What is in it (<c>~chest.available_amount</c>): credits in a credit chest, items in a furni chest.</summary>
    public int AvailableAmount => Kind == WiredChestKind.Coins ? Summary.Coins : Summary.ItemCount;

    /// <summary>Whether wired may take from or put into this chest right now.</summary>
    public bool IsUsableByWired => IsWiredEnabled && !IsLocked;

    public bool IsStarter =>
        _ctx.Definition.Name.Contains(
            _roomGrain._wiredChestConfig.StarterInfix,
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>How full the owner lets the chest get, within what its level allows.</summary>
    public int Capacity =>
        Math.Min(Summary.MaxCapacity, Number(WiredChestData.CAPACITY, Summary.MaxCapacity));

    public WiredChestSettingsSnapshot Settings =>
        new()
        {
            Kind = Kind,
            OwnerId = _ctx.RoomObject.OwnerId,
            RoomId = _ctx.RoomId,
            IsStarter = IsStarter,
            PreviewMode = (WiredChestPreviewMode)Number(WiredChestData.PREVIEW_MODE, 0),
            PreviewAmount = Number(WiredChestData.PREVIEW_AMOUNT, 1),
        };

    private IWiredChestGrain Chest => _roomGrain._grainFactory.GetWiredChestGrain(_ctx.ObjectId);

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        try
        {
            await ApplySummaryAsync(await Chest.GetSummaryAsync(Settings, ct), refresh: false);
        }
        catch (Exception ex)
        {
            // The chest still stands and opens; its counts show once its grain answers.
            _roomGrain._logger.LogError(
                ex,
                "Failed to read wired chest {ChestId} in room {RoomId}",
                _ctx.ObjectId,
                _ctx.RoomId
            );
        }
    }

    public override async Task OnPlaceAsync(ActionContext ctx, CancellationToken ct)
    {
        await SetMapDataAsync(
            new Dictionary<string, string> { [WiredChestData.LOCKED] = WiredChestData.TRUE },
            refresh: true
        );

        await base.OnPlaceAsync(ctx, ct);
    }

    /// <summary>A double-click opens the window, for whoever may look inside.</summary>
    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!await CanOpenAsync(ctx))
            return;

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new OpenChestMessageComposer { ChestId = _ctx.ObjectId },
            ct
        );
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        switch (interaction)
        {
            case OpenChestInteraction:
                if (!await CanOpenAsync(ctx))
                    return Reject(ctx, interaction, "may not look inside");

                await _roomGrain.WiredChestSystem.CloseOtherChestsAsync(ctx.PlayerId, this, ct);
                await SetViewerCountAsync(await Chest.OpenAsync(Settings, ctx.PlayerId, ct));

                return true;
            case CloseChestInteraction:
                await SetViewerCountAsync(await Chest.CloseAsync(ctx.PlayerId, ct));

                return true;
            case StartChestDepositInteraction:
                if (!await CanDepositAsync(ctx))
                    return Reject(ctx, interaction, "may not deposit");

                // A tell: the trade grain awaits this room when the deposit is confirmed.
                _roomGrain
                    ._grainFactory.GetWiredTradeGrain(ctx.PlayerId)
                    .StartChestDepositAsync(_ctx.RoomId, _ctx.ObjectId, Kind, ct)
                    .LogAndForget(
                        _roomGrain._logger,
                        "start a deposit into wired chest {ChestId}",
                        _ctx.ObjectId
                    );

                return true;
            case WithdrawFromChestInteraction withdraw:
                return await WithdrawAsync(ctx, withdraw, ct);
            case SetChestOptionsInteraction options:
                return await SetOptionsAsync(ctx, options);
            case SetChestPreferencesInteraction preferences:
                return await SetPreferencesAsync(ctx, preferences, ct);
            case SetChestNotificationPreferencesInteraction notifications:
                return await SetNotificationPreferencesAsync(ctx, notifications, ct);
            case UpgradeChestInteraction upgrade:
                return await UpgradeAsync(ctx, upgrade, ct);
            default:
                return false;
        }
    }

    /// <summary>
    /// Puts a player's offered items in, at the end of their trade with the chest. The rules are
    /// checked again here: the trade only held snapshots, and the room or the chest may have
    /// changed while it was open.
    /// </summary>
    public async Task<WiredChestMoveResultSnapshot> DepositAsync(
        ActionContext ctx,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    )
    {
        if (!AvatarModule.TryGetPlayer(ctx.PlayerId, out var player))
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.ChestNotInRoom,
                Summary
            );

        if (!await CanDepositAsync(ctx))
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.NoOrLockedChests,
                Summary
            );

        var result = await Chest.DepositAsync(
            new WiredChestDepositRequest
            {
                Chest = Settings,
                DepositorId = ctx.PlayerId,
                DepositorName = player.Name,
                ItemIds = itemIds,
                Capacity = Capacity,
                Type = WiredTransactionType.Manual,
                DefinitionInfo = string.Empty,
            },
            ct
        );

        await ApplySummaryAsync(result.Summary, refresh: true);

        return result;
    }

    /// <summary>
    /// Wired giving from this chest to a player (the give-from-chest boxes). The box has checked
    /// <see cref="IsUsableByWired"/>; the grain checks what there is to give.
    /// </summary>
    public async Task<WiredChestMoveResultSnapshot> GiveAsync(
        PlayerId receiverId,
        string receiverName,
        int? amount,
        ChestItemTypeSnapshot? itemType,
        WiredChestIterationMode order,
        WiredTransactionType type,
        string definitionInfo,
        CancellationToken ct
    )
    {
        var result = await Chest.WithdrawAsync(
            new WiredChestWithdrawRequest
            {
                Chest = Settings,
                ReceiverId = receiverId,
                ReceiverName = receiverName,
                Amount = amount,
                ItemType = itemType,
                Order = order,
                Type = type,
                DefinitionInfo = definitionInfo,
            },
            ct
        );

        await ApplySummaryAsync(result.Summary, refresh: true);

        return result;
    }

    /// <summary>
    /// A player's payment to a contract going into this chest. The contract's box checked the
    /// chest may be used by wired; the player's own rights do not matter here.
    /// </summary>
    public async Task<WiredChestMoveResultSnapshot> TakePaymentAsync(
        PlayerId depositorId,
        string depositorName,
        ImmutableArray<RoomObjectId> itemIds,
        WiredTransactionType type,
        string definitionInfo,
        CancellationToken ct
    )
    {
        var result = await Chest.DepositAsync(
            new WiredChestDepositRequest
            {
                Chest = Settings,
                DepositorId = depositorId,
                DepositorName = depositorName,
                ItemIds = itemIds,
                Capacity = Capacity,
                Type = type,
                DefinitionInfo = definitionInfo,
            },
            ct
        );

        await ApplySummaryAsync(result.Summary, refresh: true);

        return result;
    }

    /// <summary>How much more the chest takes before it is full: items, or credits.</summary>
    public int FreeCapacity =>
        Math.Max(0, Capacity - (Kind == WiredChestKind.Coins ? Summary.Coins : Summary.ItemCount));

    /// <summary>Locks or unlocks from the wired menu, which acts on many chests at once.</summary>
    public Task SetLockedAsync(bool locked) =>
        Flag(WiredChestData.LOCKED) == locked
            ? Task.CompletedTask
            : SetMapDataAsync(
                new Dictionary<string, string>
                {
                    [WiredChestData.LOCKED] = WiredChestData.Flag(locked),
                },
                refresh: true
            );

    public bool AutoLocks => Flag(WiredChestData.AUTO_LOCK);

    /// <summary>A player who had the window open left the room, or opened another chest.</summary>
    public async Task ForgetViewerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_viewerCount == 0)
            return;

        await SetViewerCountAsync(await Chest.CloseAsync(playerId, ct));
    }

    private async Task<bool> WithdrawAsync(
        ActionContext ctx,
        WithdrawFromChestInteraction withdraw,
        CancellationToken ct
    )
    {
        if (!await CanWithdrawAsync(ctx))
            return Reject(ctx, withdraw, "may not withdraw");

        if (!AvatarModule.TryGetPlayer(ctx.PlayerId, out var player))
            return Reject(ctx, withdraw, "not in the room");

        var result = await Chest.WithdrawAsync(
            new WiredChestWithdrawRequest
            {
                Chest = Settings,
                ReceiverId = ctx.PlayerId,
                ReceiverName = player.Name,
                Amount = withdraw.Amount,
                ItemType = withdraw.ItemType,
                Order = WiredChestIterationMode.FirstInFirstOut,
                Type = WiredTransactionType.Manual,
                DefinitionInfo = string.Empty,
            },
            ct
        );

        await ApplySummaryAsync(result.Summary, refresh: true);
        await SendAsync(
            ctx.PlayerId,
            result.Failure is { } failure
                ? new WiredTransactionFailMessageComposer { FailureType = failure }
                : new WiredTransactionSuccessMessageComposer
                {
                    Contents = new()
                    {
                        Type = WiredTransactionSuccessType.Withdraw,
                        RewardContents = null,
                        RewardText = null,
                        OpenByDefault = false,
                    },
                },
            ct
        );

        return result.Succeeded;
    }

    private async Task<bool> SetOptionsAsync(ActionContext ctx, SetChestOptionsInteraction options)
    {
        if (!IsItemOwner(ctx))
        {
            // Anyone who may use the chest, and the room's owner, may lock it; nothing else.
            if (
                !options.Locked
                || IsLocked
                || !(await CanEditAsync(ctx) || await SecurityModule.GetIsRoomOwnerAsync(ctx))
            )
                return Reject(ctx, options, "only the chest's owner changes this");

            await SetLockedAsync(true);

            return true;
        }

        await SetMapDataAsync(
            new Dictionary<string, string>
            {
                [WiredChestData.LOCKED] = WiredChestData.Flag(options.Locked),
                [WiredChestData.AUTO_LOCK] = WiredChestData.Flag(options.AutoLock),
                [WiredChestData.CAPACITY] = Math.Clamp(options.Capacity, 0, Summary.MaxCapacity)
                    .ToString(CultureInfo.InvariantCulture),
            },
            refresh: true
        );

        return true;
    }

    private async Task<bool> SetPreferencesAsync(
        ActionContext ctx,
        SetChestPreferencesInteraction preferences,
        CancellationToken ct
    )
    {
        var config = _roomGrain._wiredChestConfig;

        if (!IsItemOwner(ctx))
            return Reject(ctx, preferences, "not the chest's owner");

        if (
            preferences.Name.Length > config.NameMaxLength
            || preferences.Description.Length > config.DescriptionMaxLength
        )
            return Reject(ctx, preferences, "name or description too long");

        if (
            !Enum.IsDefined(preferences.StateMode)
            || !Enum.IsDefined(preferences.PreviewMode)
            || preferences.PreviewAmount < 1
            || preferences.PreviewAmount > config.MaxPreviewItems
        )
            return Reject(ctx, preferences, "unknown appearance setting");

        // Becoming a wired chest cannot be undone, and a starter chest never becomes one.
        if (preferences.WiredEnabled && !IsWiredEnabled && IsStarter)
            return Reject(ctx, preferences, "a starter chest cannot become a wired chest");

        await SetMapDataAsync(
            new Dictionary<string, string>
            {
                [WiredChestData.NAME] = preferences.Name,
                [WiredChestData.DESCRIPTION] = preferences.Description,
                [WiredChestData.EVERYONE_CAN_OPEN] = WiredChestData.Flag(
                    preferences.EveryoneCanOpen
                ),
                [WiredChestData.EVERYONE_CAN_DONATE] = WiredChestData.Flag(
                    preferences.EveryoneCanDonate
                ),
                [WiredChestData.STATE_CONTROL_MODE] = Format((int)preferences.StateMode),
                [WiredChestData.PREVIEW_MODE] = Format((int)preferences.PreviewMode),
                [WiredChestData.PREVIEW_AMOUNT] = Format(preferences.PreviewAmount),
                [WiredChestData.IS_WIRED_ENABLED] = WiredChestData.Flag(
                    IsWiredEnabled || preferences.WiredEnabled
                ),
            },
            refresh: false
        );

        // The preview depends on the settings just saved.
        await ApplySummaryAsync(await Chest.GetSummaryAsync(Settings, ct), refresh: true);
        await SendAsync(
            ctx.PlayerId,
            new ChestPreferencesUpdateSuccessMessageComposer
            {
                ChestId = _ctx.ObjectId,
                IsNotificationPreferences = false,
            },
            ct
        );

        return true;
    }

    private async Task<bool> SetNotificationPreferencesAsync(
        ActionContext ctx,
        SetChestNotificationPreferencesInteraction notifications,
        CancellationToken ct
    )
    {
        if (!IsItemOwner(ctx))
            return Reject(ctx, notifications, "not the chest's owner");

        if (!Enum.IsDefined(notifications.NotifyMode))
            return Reject(ctx, notifications, "unknown notify mode");

        await SetMapDataAsync(
            new Dictionary<string, string>
            {
                [WiredChestData.NOTIFY_MODE] = Format((int)notifications.NotifyMode),
                [WiredChestData.NOTIFICATION_CHEST_FULL] = WiredChestData.Flag(
                    notifications.OnChestFull
                ),
                [WiredChestData.NOTIFICATION_DONATION] = WiredChestData.Flag(
                    notifications.OnDonation
                ),
                [WiredChestData.NOTIFICATION_SOMEONE_WITHDRAWS] = WiredChestData.Flag(
                    notifications.OnWithdraw
                ),
                [WiredChestData.NOTIFICATION_CHEST_EMPTY] = WiredChestData.Flag(
                    notifications.OnChestEmpty
                ),
                [WiredChestData.NOTIFICATION_WIRED_TRANSACTION] = WiredChestData.Flag(
                    notifications.OnWiredTransaction
                ),
            },
            refresh: true
        );

        await SendAsync(
            ctx.PlayerId,
            new ChestPreferencesUpdateSuccessMessageComposer
            {
                ChestId = _ctx.ObjectId,
                IsNotificationPreferences = true,
            },
            ct
        );

        return true;
    }

    private async Task<bool> UpgradeAsync(
        ActionContext ctx,
        UpgradeChestInteraction upgrade,
        CancellationToken ct
    )
    {
        var (result, summary) = await Chest.UpgradeAsync(
            Settings,
            ctx.PlayerId,
            upgrade.Upgrades,
            ct
        );

        await ApplySummaryAsync(summary, refresh: true);
        await SendAsync(
            ctx.PlayerId,
            new UpgradeChestResultMessageComposer { ChestId = _ctx.ObjectId, Result = result },
            ct
        );

        return result == UpgradeChestResultType.Success;
    }

    /// <summary>The owner, and on a wired chest whoever may modify wired here.</summary>
    private async Task<bool> CanEditAsync(ActionContext ctx)
    {
        if (IsItemOwner(ctx))
            return true;

        if (!IsWiredEnabled)
            return false;

        var (canModify, _) = WiredSystem.GetPermissions(
            await SecurityModule.GetControllerLevelAsync(ctx)
        );

        return canModify;
    }

    private async Task<bool> CanOpenAsync(ActionContext ctx)
    {
        if (IsItemOwner(ctx) || Flag(WiredChestData.EVERYONE_CAN_OPEN))
            return true;

        var (_, canRead) = WiredSystem.GetPermissions(
            await SecurityModule.GetControllerLevelAsync(ctx)
        );

        return canRead;
    }

    private async Task<bool> CanWithdrawAsync(ActionContext ctx) =>
        (IsItemOwner(ctx) || !IsLocked) && await CanEditAsync(ctx);

    private async Task<bool> CanDepositAsync(ActionContext ctx) =>
        Flag(WiredChestData.EVERYONE_CAN_DONATE) || await CanWithdrawAsync(ctx);

    private Task SetViewerCountAsync(int viewerCount)
    {
        _viewerCount = viewerCount;

        return ApplyStateAsync();
    }

    /// <summary>Draws the chest open or closed as its state mode says.</summary>
    private Task ApplyStateAsync()
    {
        int? state = (WiredChestStateMode)Number(WiredChestData.STATE_CONTROL_MODE, 0) switch
        {
            WiredChestStateMode.OpenWhenViewed => _viewerCount > 0
                ? WiredChestData.OPEN_STATE
                : WiredChestData.CLOSED_STATE,
            WiredChestStateMode.AlwaysOpen => WiredChestData.OPEN_STATE,
            WiredChestStateMode.AlwaysClosed => WiredChestData.CLOSED_STATE,
            _ => null,
        };

        return state is { } value && value != GetState()
            ? SetStateAsync(value)
            : Task.CompletedTask;
    }

    /// <summary>Mirrors what the chest holds into the stuff data the client draws from.</summary>
    private async Task ApplySummaryAsync(WiredChestSummarySnapshot summary, bool refresh)
    {
        Summary = summary;

        var entries = new Dictionary<string, string>
        {
            [WiredChestData.CONTENTS_COUNT] = Format(
                Kind == WiredChestKind.Coins ? summary.Coins : summary.ItemCount
            ),
            [WiredChestData.CAPACITY_LEVEL] = Format(summary.CapacityLevel),
            [WiredChestData.VISUALS] = WiredChestData.FormatVisuals(summary.Preview),
        };

        if (StuffData is IMapStuffData map && map.ValueOf(WiredChestData.CAPACITY).Length == 0)
            entries[WiredChestData.CAPACITY] = Format(summary.MaxCapacity);

        if (!Changes(entries))
        {
            await ApplyStateAsync();

            return;
        }

        await SetMapDataAsync(entries, refresh);
        await ApplyStateAsync();
    }

    private bool Changes(IReadOnlyDictionary<string, string> entries)
    {
        if (StuffData is not IMapStuffData map)
            return false;

        foreach (var (key, value) in entries)
        {
            if (map.ValueOf(key) != value)
                return true;
        }

        return false;
    }

    private bool Flag(string key) =>
        StuffData is IMapStuffData map && map.ValueOf(key) == WiredChestData.TRUE;

    private int Number(string key, int fallback) =>
        StuffData is IMapStuffData map
        && int.TryParse(
            map.ValueOf(key),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value
        )
            ? value
            : fallback;

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private Task SendAsync(PlayerId playerId, IComposer composer, CancellationToken ct) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(playerId, composer, ct);
}
