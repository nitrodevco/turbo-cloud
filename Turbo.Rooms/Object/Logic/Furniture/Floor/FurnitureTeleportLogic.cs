using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A teleporter, one half of a linked pair (<see cref="RoomLinkerData.ItemId"/> names the
/// other half; the room it stands in is looked up when it is used). It runs the sequence the
/// client animates, one <c>RoomConfig.TeleportStepMs</c> step at a time:
/// <list type="number">
/// <item>Used from anywhere, it walks the player to the tile in front of it.</item>
/// <item>It opens (<see cref="TeleportStates.OPEN"/>), the player steps in, it shuts.</item>
/// <item>It flashes (<see cref="TeleportStates.ACTIVE"/>) and hands the player over: to the
/// other half in this room, which flashes, opens and lets them walk out; or, for a half in
/// another room, forwards them there, where they arrive inside it
/// (<see cref="IRoomArrivalLogic"/>) past the door but not past bans or capacity.</item>
/// <item>With no usable other half (never paired, in an inventory, busy), it opens again and
/// lets them back out.</item>
/// </list>
/// One player at a time; while it holds them their own walk requests are refused
/// (<see cref="IRoomAvatar.IsTeleporting"/>). Every step re-checks that the player is still
/// where it left them, and lets go the moment they are not.
/// </summary>
[RoomObjectLogic(TeleportFurniture.LOGIC_NAME)]
public class FurnitureTeleportLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx),
        IRoomArrivalLogic
{
    // Who is walking up to it and when it stops waiting for them; they are not held yet, so a
    // later click from someone else simply takes over.
    private RoomObjectId? _approachingId;
    private long _waitUntilMs;

    // Who it holds, from stepping in until they have walked out or left.
    private RoomObjectId? _occupantId;

    private int StepMs => _roomGrain._roomConfig.TeleportStepMs;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    // Walkable only while it stands open for someone stepping in or out.
    public override bool CanWalk() => GetState() == TeleportStates.OPEN;

    public bool CanReceiveArrival => !IsBusy;

    private bool IsBusy => _occupantId is not null;

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        // Nobody is inside a teleporter that has just been placed or loaded.
        if (GetState() != TeleportStates.CLOSED)
            await SetStateAsync(TeleportStates.CLOSED, refresh: false);

        await base.OnAttachAsync(ct);
    }

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _approachingId = null;

        Release();

        await base.OnPickupAsync(ctx, ct);
    }

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (IsBusy || GetAvatar(ctx) is not { IsTeleporting: false } avatar)
            return;

        var map = MapModule;
        var tileIdx = _ctx.GetTileIdx();

        if (!TryGetFrontIdx(out var frontIdx))
            return;

        var avatarIdx = map.ToIdx(avatar.X, avatar.Y);

        if (avatarIdx == tileIdx)
            return;

        if (avatarIdx == frontIdx && !avatar.IsWalking)
        {
            await StepInAsync(avatar, ct);

            return;
        }

        var (frontX, frontY) = map.GetTileXY(frontIdx);

        if (!await AvatarModule.WalkAvatarToAsync(avatar, frontX, frontY, ct))
            return;

        _approachingId = avatar.ObjectId;
        _waitUntilMs = _roomGrain.NowMs() + _roomGrain._roomConfig.TeleportWalkTimeoutMs;

        Schedule(_roomGrain._roomConfig.AvatarTickMs, WaitForApproachAsync);
    }

    public virtual async Task ReceiveArrivalAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        Hold(avatar);

        await SetStateAsync(TeleportStates.ACTIVE);

        Schedule(StepMs, OpenToLetOutAsync);
    }

    /// <summary>
    /// Takes a player handed over by the other half in this room. False when it is busy, and
    /// the sender lets them back out instead.
    /// </summary>
    private bool TryReserve(IRoomAvatar avatar)
    {
        if (IsBusy)
            return false;

        Hold(avatar);

        return true;
    }

    private async Task WaitForApproachAsync(CancellationToken ct)
    {
        if (
            IsBusy
            || !TryGetAvatar(_approachingId, out var avatar)
            || !TryGetFrontIdx(out var frontIdx)
        )
        {
            _approachingId = null;

            return;
        }

        var map = MapModule;

        if (map.ToIdx(avatar.X, avatar.Y) == frontIdx && !avatar.IsWalking)
        {
            _approachingId = null;

            await StepInAsync(avatar, ct);

            return;
        }

        // Still on the way; anyone who stopped somewhere else, or took too long, changed
        // their mind.
        if (avatar.IsWalking && _roomGrain.NowMs() < _waitUntilMs)
        {
            Schedule(_roomGrain._roomConfig.AvatarTickMs, WaitForApproachAsync);

            return;
        }

        _approachingId = null;
    }

    private async Task StepInAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        Hold(avatar);

        await SetStateAsync(TeleportStates.OPEN);

        var (x, y) = MapModule.GetTileXY(_ctx.GetTileIdx());

        if (!await AvatarModule.WalkAvatarToAsync(avatar, x, y, ct))
        {
            await CloseAndReleaseAsync();

            return;
        }

        _waitUntilMs = _roomGrain.NowMs() + _roomGrain._roomConfig.TeleportWalkTimeoutMs;

        Schedule(_roomGrain._roomConfig.AvatarTickMs, WaitForInsideAsync);
    }

    private async Task WaitForInsideAsync(CancellationToken ct)
    {
        if (!TryGetOccupant(out var avatar))
        {
            await CloseAndReleaseAsync();

            return;
        }

        if (IsInside(avatar) && !avatar.IsWalking)
        {
            await SetStateAsync(TeleportStates.CLOSED);

            Schedule(StepMs, SendAsync);

            return;
        }

        if (avatar.IsWalking && _roomGrain.NowMs() < _waitUntilMs)
        {
            Schedule(_roomGrain._roomConfig.AvatarTickMs, WaitForInsideAsync);

            return;
        }

        await CloseAndReleaseAsync();
    }

    private async Task SendAsync(CancellationToken ct)
    {
        if (!TryGetOccupant(out var avatar) || !IsInside(avatar))
        {
            await CloseAndReleaseAsync();

            return;
        }

        await SetStateAsync(TeleportStates.ACTIVE);

        var partnerId = GetPartnerId();
        var partnerRoomId =
            partnerId > 0 ? await FurniModule.GetRoomIdOfItemAsync(partnerId, ct) : null;

        if (partnerRoomId == _roomGrain.RoomId)
        {
            if (
                FurniModule.TryGetFloorItem(partnerId, out var partnerItem)
                && partnerItem.Logic is FurnitureTeleportLogic partner
                && partner.TryReserve(avatar)
            )
            {
                Schedule(StepMs, token => HandOverAsync(partner, token));

                return;
            }
        }
        else if (partnerRoomId is { } otherRoomId && avatar is IRoomPlayer player)
        {
            Schedule(StepMs, token => ForwardAsync(player, otherRoomId, partnerId, token));

            return;
        }

        // Nowhere to go: it opens again and lets them back out.
        Schedule(StepMs, OpenToLetOutAsync);
    }

    private async Task HandOverAsync(FurnitureTeleportLogic partner, CancellationToken ct)
    {
        if (!TryGetOccupant(out var avatar) || !IsInside(avatar))
        {
            partner.Release();
            await CloseAndReleaseAsync();

            return;
        }

        await AvatarModule.RelocateAvatarAsync(avatar, partner._ctx.GetTileIdx(), ct);

        avatar.SetRotation(partner._ctx.RoomObject.Rotation);
        avatar.MarkDirty();

        // The player is the other half's now; this one shuts behind them.
        _occupantId = null;

        await SetStateAsync(TeleportStates.CLOSED);
        await partner.ReceiveArrivalAsync(avatar, ct);
    }

    private async Task ForwardAsync(
        IRoomPlayer player,
        RoomId roomId,
        int partnerId,
        CancellationToken ct
    )
    {
        if (!TryGetOccupant(out var avatar) || !IsInside(avatar))
        {
            await CloseAndReleaseAsync();

            return;
        }

        // Told, not awaited: this runs in the room turn. The presence records that they arrive
        // through the other half, which also lets them past that room's door.
        _roomGrain
            ._grainFactory.ForwardPlayerToRoomAsync(
                player.PlayerId,
                roomId,
                new RoomEntrySnapshot
                {
                    Method = RoomEntryMethodType.Teleport,
                    TeleportId = partnerId,
                },
                CancellationToken.None
            )
            .LogAndForget(
                _roomGrain._logger,
                "teleport player {PlayerId} to room {RoomId}",
                player.PlayerId,
                roomId
            );

        _waitUntilMs = _roomGrain.NowMs() + _roomGrain._roomConfig.TeleportForwardTimeoutMs;

        Schedule(StepMs, WaitForDepartureAsync);
    }

    private async Task WaitForDepartureAsync(CancellationToken ct)
    {
        // Gone to the other room: it shuts behind them.
        if (!TryGetOccupant(out var avatar))
        {
            await CloseAndReleaseAsync();

            return;
        }

        if (_roomGrain.NowMs() < _waitUntilMs)
        {
            Schedule(StepMs, WaitForDepartureAsync);

            return;
        }

        // Still here: the other room would not take them (banned, full). Let them back out.
        await OpenToLetOutAsync(ct);
    }

    private async Task OpenToLetOutAsync(CancellationToken ct)
    {
        if (!TryGetOccupant(out _))
        {
            await CloseAndReleaseAsync();

            return;
        }

        await SetStateAsync(TeleportStates.OPEN);

        Schedule(StepMs, WalkOutAsync);
    }

    private async Task WalkOutAsync(CancellationToken ct)
    {
        if (
            !TryGetOccupant(out var avatar)
            || !IsInside(avatar)
            || !TryGetFrontIdx(out var frontIdx)
        )
        {
            await CloseAndReleaseAsync();

            return;
        }

        var (frontX, frontY) = MapModule.GetTileXY(frontIdx);

        // Someone standing in front blocks the way; the player stays inside and may walk off
        // once they are let go.
        if (!await AvatarModule.WalkAvatarToAsync(avatar, frontX, frontY, ct))
        {
            await CloseAndReleaseAsync();

            return;
        }

        _waitUntilMs = _roomGrain.NowMs() + _roomGrain._roomConfig.TeleportWalkTimeoutMs;

        Schedule(_roomGrain._roomConfig.AvatarTickMs, WaitForOutsideAsync);
    }

    private async Task WaitForOutsideAsync(CancellationToken ct)
    {
        if (TryGetOccupant(out var avatar) && IsInside(avatar) && _roomGrain.NowMs() < _waitUntilMs)
        {
            Schedule(_roomGrain._roomConfig.AvatarTickMs, WaitForOutsideAsync);

            return;
        }

        await CloseAndReleaseAsync();
    }

    private async Task CloseAndReleaseAsync()
    {
        Release();

        if (GetState() != TeleportStates.CLOSED)
            await SetStateAsync(TeleportStates.CLOSED);
    }

    private void Hold(IRoomAvatar avatar)
    {
        _occupantId = avatar.ObjectId;
        avatar.IsTeleporting = true;
    }

    private void Release()
    {
        if (TryGetOccupant(out var avatar))
            avatar.IsTeleporting = false;

        _occupantId = null;
    }

    private bool TryGetOccupant([NotNullWhen(true)] out IRoomAvatar? avatar) =>
        TryGetAvatar(_occupantId, out avatar);

    private bool TryGetAvatar(RoomObjectId? objectId, [NotNullWhen(true)] out IRoomAvatar? avatar)
    {
        avatar = null;

        return objectId is { } id && AvatarModule.TryGetAvatar(id, out avatar);
    }

    private bool IsInside(IRoomAvatar avatar) =>
        MapModule.ToIdx(avatar.X, avatar.Y) == _ctx.GetTileIdx();

    private bool TryGetFrontIdx(out int frontIdx) =>
        MapModule.TryGetTileInFront(_ctx.GetTileIdx(), _ctx.RoomObject.Rotation, out frontIdx);

    /// <summary>The item this half leads to (<c>~teleport.target_id</c>); 0 when it leads nowhere.</summary>
    public int PartnerItemId => GetPartnerId();

    /// <summary>
    /// Points this half at another item - what writing <c>~teleport.target_id</c> does (to another
    /// linker of the room, as the Wired Faculty describes it). Only this half changes.
    /// </summary>
    public void LinkTo(int itemId) =>
        _ctx.RoomObject.ExtraData.UpdateSection(
            RoomLinkerData.SECTION,
            new RoomLinkerData { ItemId = itemId }
        );

    private int GetPartnerId() =>
        FurnitureExtraDataSections
            .Read<RoomLinkerData>(
                _ctx.RoomObject.ExtraData,
                _ctx.Definition.ExtraData,
                RoomLinkerData.SECTION,
                _roomGrain._logger
            )
            ?.ItemId
        ?? 0;

    private void Schedule(int delayMs, Func<CancellationToken, Task> step) =>
        TimerSystem.Schedule(_ctx.ObjectId, delayMs, step);
}
