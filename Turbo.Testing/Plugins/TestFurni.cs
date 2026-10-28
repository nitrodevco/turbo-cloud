using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Testing;

/// <summary>
/// A furni in a <see cref="TestRoom"/> running a plugin's <see cref="FurniLogic"/>: the test walks
/// players on and off it, uses it and sends it messages, and reads what it did (its state, what
/// it said, the wired it fired, where it sent players). The room is an open floor of
/// <see cref="Width"/> by <see cref="Length"/> tiles; a walk to tiles arrives at once. Its extra
/// data sections are the test's to set (<see cref="ExtraData"/>).
/// </summary>
public sealed class TestFurni : IFurniContext
{
    private readonly TestHotel _hotel;
    private readonly FurniLogic _logic;
    private readonly List<(TimeSpan Delay, Func<CancellationToken, Task> Run)> _scheduled = [];
    private readonly List<(RoomUser User, RoomId Room, RoomEntryExtension? Extension)> _forwarded =
    [];
    private bool _attached;

    internal TestFurni(
        TestHotel hotel,
        TestRoom room,
        FurniLogic logic,
        int id,
        string logicKey,
        int x,
        int y,
        int state
    )
    {
        _hotel = hotel;
        _logic = logic;
        TestRoom = room;
        Self = new RoomFurni(
            id,
            logicKey,
            x,
            y,
            0,
            state,
            Rotation.North,
            Definition(id, logicKey)
        );
    }

    public TestRoom TestRoom { get; }

    public RoomFurni Self { get; private set; }

    /// <summary>The furni's state as its logic last set it.</summary>
    public int State => Self.State;

    public int ObjectExtra { get; private set; }

    /// <summary>The furni's extra data, by section, as JSON: the test's to set.</summary>
    public Dictionary<string, string> ExtraData { get; } = [];

    /// <summary>The players it sent to another room, in order.</summary>
    public IReadOnlyList<(RoomUser User, RoomId Room, RoomEntryExtension? Extension)> Forwarded =>
        _forwarded;

    /// <summary>What it asked to run later (<see cref="IFurniContext.Schedule"/>); <see cref="RunScheduledAsync"/> runs it.</summary>
    public int Scheduled => _scheduled.Count;

    public int Width { get; set; } = 10;

    public int Length { get; set; } = 10;

    public int PairedItemId { get; set; }

    RoomId IRoomTurn.Room => TestRoom.Id;

    public IReadOnlyList<RoomUser> RoomUsers =>
        [.. TestRoom.Players.Select((p, i) => UserOf(p, i))];

    public IRoomMap Map => new OpenMap(this);

    public RoomTilePoint? TileInFront => new RoomTilePoint(Self.X, Self.Y + 1);

    /// <summary>The player as the room hands them to furni logic.</summary>
    public RoomUser UserOf(TestPlayer player) =>
        UserOf(player, TestRoom.Players.ToList().IndexOf(player));

    private RoomUser UserOf(TestPlayer player, int index) =>
        new(
            Math.Max(index, 0),
            RoomUserKind.Player,
            player.Id,
            player.Name,
            Self.X,
            Self.Y,
            Rotation.South,
            false,
            false,
            0
        );

    /// <summary>The player walks onto the furni (its logic's <c>OnWalkOnAsync</c>).</summary>
    public async Task WalkOnAsync(TestPlayer player, CancellationToken ct = default)
    {
        await AttachAsync(ct).ConfigureAwait(false);
        await _logic.OnWalkOnAsync(this, UserOf(player), ct).ConfigureAwait(false);
    }

    /// <summary>The player walks off the furni (its logic's <c>OnWalkOffAsync</c>).</summary>
    public async Task WalkOffAsync(TestPlayer player, CancellationToken ct = default)
    {
        await AttachAsync(ct).ConfigureAwait(false);
        await _logic.OnWalkOffAsync(this, UserOf(player), ct).ConfigureAwait(false);
    }

    /// <summary>The player uses the furni (double-click, <paramref name="param"/> the client's); whether the logic handled it.</summary>
    public async Task<bool> UseAsync(
        TestPlayer? player,
        int param = 0,
        CancellationToken ct = default
    )
    {
        await AttachAsync(ct).ConfigureAwait(false);

        return await _logic
            .OnUseAsync(this, player is null ? null : UserOf(player), param, ct)
            .ConfigureAwait(false);
    }

    /// <summary>A message to the furni, as a plugin's handler sends one (<c>IHotelRooms.InvokeFurniLogicAsync</c>).</summary>
    public async Task<FurniLogicReply> SendAsync(
        FurniLogicMessage message,
        TestPlayer? sender = null,
        CancellationToken ct = default
    )
    {
        await AttachAsync(ct).ConfigureAwait(false);

        return await _logic
            .OnMessageAsync(this, message, sender is null ? null : UserOf(sender), ct)
            .ConfigureAwait(false);
    }

    /// <summary>Runs what the logic scheduled, earliest first, as its delays come due.</summary>
    public async Task RunScheduledAsync(CancellationToken ct = default)
    {
        while (_scheduled.Count > 0)
        {
            var next = _scheduled.OrderBy(x => x.Delay).First();

            _scheduled.Remove(next);
            _hotel.Clock.Advance(next.Delay);
            await next.Run(ct).ConfigureAwait(false);
        }
    }

    /// <summary>The room takes the furni away (its logic's <c>OnDetachedAsync</c>).</summary>
    public async Task DetachAsync(CancellationToken ct = default)
    {
        if (_attached)
            await _logic.OnDetachedAsync(this, ct).ConfigureAwait(false);

        _attached = false;
    }

    private async Task AttachAsync(CancellationToken ct)
    {
        if (_attached)
            return;

        _attached = true;
        await _logic.OnAttachedAsync(this, ct).ConfigureAwait(false);
    }

    public Task SetStateAsync(int state, CancellationToken ct)
    {
        Self = Self with { State = state };

        return Task.CompletedTask;
    }

    public Task FireWiredAsync<TEvent>(TEvent evt, RoomUser? triggerer, CancellationToken ct)
        where TEvent : IWiredEvent =>
        _hotel.Wired.FireAsync(TestRoom.Id, evt, triggerer?.Player, ct);

    public T? ReadExtraData<T>(string section)
        where T : class =>
        ExtraData.TryGetValue(section, out var json) ? JsonSerializer.Deserialize<T>(json) : null;

    public string? ReadExtraDataJson(string section) => ExtraData.GetValueOrDefault(section);

    public Task<RoomId?> GetRoomOfItemAsync(int itemId, CancellationToken ct) =>
        Task.FromResult<RoomId?>(itemId == Self.Id ? TestRoom.Id : null);

    public Task SetObjectExtraAsync(int extra, CancellationToken ct)
    {
        ObjectExtra = extra;

        return Task.CompletedTask;
    }

    public async Task<bool> WalkToAsync(
        RoomUser user,
        IReadOnlyList<RoomTilePoint> tiles,
        Func<RoomUser, CancellationToken, Task> onArrived,
        CancellationToken ct
    )
    {
        if (tiles.Count == 0)
            return false;

        await onArrived(user with { X = tiles[0].X, Y = tiles[0].Y }, ct).ConfigureAwait(false);

        return true;
    }

    public Task<bool> WalkToInOrderAsync(
        RoomUser user,
        IReadOnlyList<RoomTilePoint> tiles,
        Func<RoomUser, CancellationToken, Task> onArrived,
        CancellationToken ct
    ) => WalkToAsync(user, tiles, onArrived, ct);

    public IReadOnlyList<RoomTilePoint> TilesBeside(RoomUser user) =>
        [new RoomTilePoint(Self.X, Self.Y + 1)];

    public bool StandsBeside(RoomUser user) =>
        Math.Abs(user.X - Self.X) <= 1 && Math.Abs(user.Y - Self.Y) <= 1;

    public void Schedule(TimeSpan delay, Func<CancellationToken, Task> run) =>
        _scheduled.Add((delay, run));

    public void CancelSchedule() => _scheduled.Clear();

    public Task<bool> ShowStepAsync(RoomUser user, RoomTilePoint to, CancellationToken ct) =>
        ActedAsync($"ShowStep({user.Name}, {to.X}, {to.Y})");

    public Task<bool> ForwardToRoomAsync(
        RoomUser user,
        RoomId room,
        RoomEntryExtension? extension,
        CancellationToken ct
    )
    {
        _forwarded.Add((user, room, extension));

        return Task.FromResult(true);
    }

    public async Task<bool> WatchRoomAsync(
        RoomUser user,
        RoomId room,
        IPluginMessage envelope,
        TimeSpan lease,
        CancellationToken ct
    ) =>
        user.Player is { } player
        && (
            await _hotel
                .Fake.Rooms.WatchAsync(player, room, envelope, lease, ct)
                .ConfigureAwait(false)
        ) == Turbo.Primitives.Rooms.Watching.RoomWatchOutcome.Watching;

    public Task<bool> SayAsync(
        RoomUser speaker,
        string text,
        RoomSpeech speech,
        CancellationToken ct
    )
    {
        TestRoom.Say(speaker.Name, text, speech);

        return Task.FromResult(true);
    }

    public Task<bool> SetEffectAsync(RoomUser user, int effectId, CancellationToken ct) =>
        ActedAsync($"SetEffect({user.Name}, {effectId})");

    public Task<bool> StopWalkingAsync(RoomUser user, CancellationToken ct) =>
        ActedAsync($"StopWalking({user.Name})");

    public Task<bool> SetRotationAsync(RoomUser user, Rotation rotation, CancellationToken ct) =>
        ActedAsync($"SetRotation({user.Name}, {rotation})");

    public Task<bool> SetBotFigureAsync(RoomUser bot, string figure, CancellationToken ct) =>
        ActedAsync($"SetBotFigure({bot.Name}, {figure})");

    public Task<bool> SetBotFollowingAsync(RoomUser bot, RoomUser? target, CancellationToken ct) =>
        ActedAsync($"SetBotFollowing({bot.Name}, {target?.Name})");

    public Task<RoomFurni?> PlaceTemporaryFurniAsync(
        FurnitureDefinitionSnapshot definition,
        RoomTilePoint tile,
        Rotation rotation,
        CancellationToken ct
    )
    {
        TestRoom.Act($"PlaceTemporaryFurni({definition.Name}, {tile.X}, {tile.Y})");

        return Task.FromResult<RoomFurni?>(
            new RoomFurni(-1, definition.LogicName, tile.X, tile.Y, 0, 0, rotation, definition)
        );
    }

    public Task<bool> RemoveTemporaryFurniAsync(int furniId, CancellationToken ct) =>
        ActedAsync($"RemoveTemporaryFurni({furniId})");

    private Task<bool> ActedAsync(string action)
    {
        TestRoom.Act(action);

        return Task.FromResult(true);
    }

    private static FurnitureDefinitionSnapshot Definition(int id, string logic) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = logic,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            LogicName = logic,
            TotalStates = 2,
            Width = 1,
            Length = 1,
            StackHeight = Altitude.FromInt(0),
            CanStack = true,
            CanWalk = true,
            CanSit = false,
            CanLay = false,
            CanRecycle = false,
            CanTrade = true,
            CanGroup = false,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Everybody,
            ExtraData = null,
        };

    /// <summary>An open floor: every tile on it is free; only this furni stands on it.</summary>
    private sealed class OpenMap(TestFurni furni) : IRoomMap
    {
        public int Width => furni.Width;

        public int Height => furni.Length;

        public bool IsFloor(RoomTilePoint tile) =>
            tile.X >= 0 && tile.Y >= 0 && tile.X < Width && tile.Y < Height;

        public bool IsFree(RoomTilePoint tile) => IsFloor(tile);

        public bool CanStand(RoomUser user, RoomTilePoint tile) => IsFloor(tile);

        public IReadOnlyList<RoomFurni> FurniAt(RoomTilePoint tile) =>
            tile.X == furni.Self.X && tile.Y == furni.Self.Y ? [furni.Self] : [];
    }
}
