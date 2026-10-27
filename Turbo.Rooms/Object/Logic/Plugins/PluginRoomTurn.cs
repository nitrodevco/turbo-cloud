using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;

namespace Turbo.Rooms.Object.Logic.Plugins;

/// <summary>
/// A room as a plugin acts on it in the room's turn (<see cref="IRoomTurn"/>), over the room's own
/// modules: every action goes through the room's rules as core's own logic does. A temporary furni
/// it drops is owned by <paramref name="owner"/>'s answer, the owner of the furni asking.
/// </summary>
internal sealed class PluginRoomTurn(RoomGrain roomGrain, Func<PlayerId> owner)
    : RoomGrainComponent(roomGrain),
        IRoomTurn
{
    private PluginRoomMap? _map;

    public RoomId Room => State.RoomId;

    public IReadOnlyList<RoomUser> RoomUsers =>
        [.. AvatarModule.Avatars.Select(PluginRoomViews.User)];

    public IRoomMap Map => _map ??= new PluginRoomMap(_roomGrain);

    public Task<bool> SayAsync(
        RoomUser speaker,
        string text,
        RoomSpeech speech,
        CancellationToken ct
    ) => SayAsync(AvatarModule, ChatSystem, speaker, text, speech, ct);

    /// <summary>
    /// A plugin's line over a user, for furni logic and wired boxes alike. A pet is not made to
    /// speak; a whisper is a player's: a bot has no one to show it to but the whole room.
    /// </summary>
    internal static async Task<bool> SayAsync(
        RoomAvatarModule avatars,
        RoomChatSystem chat,
        RoomUser speaker,
        string text,
        RoomSpeech speech,
        CancellationToken ct
    )
    {
        if (
            speaker.Kind == RoomUserKind.Pet
            || string.IsNullOrWhiteSpace(text)
            || !avatars.TryGetAvatar(new RoomObjectId(speaker.Index), out var avatar)
        )
            return false;

        var onlyFor = avatar as IRoomPlayer;

        if (speech.OnlyForThem && onlyFor is null)
            return false;

        await chat.SayAsAvatarAsync(
            avatar,
            text,
            new AvatarSpeech
            {
                Shout = speech.Shout,
                StyleId = speech.StyleId,
                BubbleWidth = speech.BubbleWidth,
                OnlyFor = speech.OnlyForThem ? onlyFor : null,
            },
            ct
        );

        return true;
    }

    public Task<bool> SetEffectAsync(RoomUser user, int effectId, CancellationToken ct) =>
        TryGetAvatar(user, out var avatar)
            ? AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, effectId, ct)
            : Task.FromResult(false);

    public async Task<bool> StopWalkingAsync(RoomUser user, CancellationToken ct)
    {
        if (!TryGetAvatar(user, out var avatar))
            return false;

        await AvatarModule.StopWalkingAsync(avatar, ct);

        return true;
    }

    public Task<bool> SetRotationAsync(RoomUser user, Rotation rotation, CancellationToken ct)
    {
        if (!TryGetAvatar(user, out var avatar))
            return Task.FromResult(false);

        avatar.SetRotation(rotation);
        avatar.MarkDirty();

        return Task.FromResult(true);
    }

    public async Task<bool> SetBotFigureAsync(RoomUser bot, string figure, CancellationToken ct)
    {
        if (!TryGetAvatar(bot, out var avatar) || avatar is not IRoomBot roomBot)
            return false;

        await BotModule.SetFigureAsync(roomBot, figure, roomBot.Gender, ct);

        return true;
    }

    public async Task<bool> SetBotFollowingAsync(
        RoomUser bot,
        RoomUser? target,
        CancellationToken ct
    )
    {
        if (!TryGetAvatar(bot, out var avatar) || avatar is not IRoomBot roomBot)
            return false;

        if (target is null)
            return await BotModule.FollowAsync(roomBot, -1, false);

        return TryGetAvatar(target, out var followed)
            && await BotModule.FollowAsync(roomBot, followed.ObjectId, true);
    }

    public async Task<RoomFurni?> PlaceTemporaryFurniAsync(
        FurnitureDefinitionSnapshot definition,
        RoomTilePoint tile,
        Rotation rotation,
        CancellationToken ct
    ) =>
        await FurniModule.PlaceTemporaryFloorItemAsync(
            ActionContext.CreateForSystem(Room),
            definition,
            owner(),
            tile.X,
            tile.Y,
            null,
            rotation,
            ct
        )
            is { } item
            ? PluginRoomViews.Furni(item)
            : null;

    public Task<bool> RemoveTemporaryFurniAsync(int furniId, CancellationToken ct) =>
        FurniModule.TryGetItem(furniId, out var item)
            ? FurniModule.RemoveTemporaryItemAsync(ActionContext.CreateForSystem(Room), item, ct)
            : Task.FromResult(false);

    internal bool TryGetAvatar(RoomUser user, [NotNullWhen(true)] out IRoomAvatar? avatar) =>
        AvatarModule.TryGetAvatar(new RoomObjectId(user.Index), out avatar);
}

/// <summary>The room's floor as a plugin reads it (<see cref="IRoomMap"/>).</summary>
internal sealed class PluginRoomMap(RoomGrain roomGrain) : RoomGrainComponent(roomGrain), IRoomMap
{
    public int Width => MapModule.Width;

    public int Height => MapModule.Height;

    public bool IsFloor(RoomTilePoint tile) =>
        MapModule.InBounds(tile.X, tile.Y)
        && !MapModule.IsTileDisabled(MapModule.ToIdx(tile.X, tile.Y));

    // A tile is walkable by its flags only under furni one can walk on, so "nothing on it" is
    // its top item, not its flags.
    public bool IsFree(RoomTilePoint tile)
    {
        if (!IsFloor(tile))
            return false;

        var state = MapModule.GetTile(MapModule.ToIdx(tile.X, tile.Y));

        return state.Flags.Has(RoomTileFlags.Open)
            && !state.Flags.Has(RoomTileFlags.Closed, RoomTileFlags.AvatarOccupied)
            && state.Highest is null;
    }

    public bool CanStand(RoomUser user, RoomTilePoint tile) =>
        MapModule.InBounds(tile.X, tile.Y)
        && AvatarModule.TryGetAvatar(new RoomObjectId(user.Index), out var avatar)
        && MapModule.CanAvatarWalk(avatar, MapModule.ToIdx(tile.X, tile.Y));

    public IReadOnlyList<RoomFurni> FurniAt(RoomTilePoint tile) =>
        MapModule.InBounds(tile.X, tile.Y)
            ?
            [
                .. FurniModule
                    .GetFloorItemsOnTile(MapModule.ToIdx(tile.X, tile.Y))
                    .OrderBy(item => item.Z.Value)
                    .Select(PluginRoomViews.Furni),
            ]
            : [];
}
