using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Contracts.Furni;

/// <summary>
/// Names a plugin's furni logic: the floor furniture whose logic is <paramref name="key"/> in the
/// hotel's furniture definitions runs it. Found as the plugin loads; the furniture goes back to its
/// old logic when it unloads. A key core already has is replaced, and the inspector says so.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FurniLogicAttribute(string key) : Attribute
{
    public string Key { get; } = key;

    /// <summary>
    /// Sold, granted and refused as a gift as a linked pair, exactly as a teleporter is: one bought
    /// is two, each half naming the other in its <c>room_linker</c> extra data
    /// (<see cref="IFurniContext.PairedItemId"/>). While the plugin is loaded. A player it sends to
    /// the room its other half stands in (<see cref="IFurniContext.ForwardToRoomAsync"/>) passes
    /// that room's door as a teleporter's does.
    /// </summary>
    public bool LinkedPair { get; init; }
}

/// <summary>
/// A plugin's floor furni logic, built against the contracts only. Everything else (walking,
/// stacking, sitting, rolling, the definition's own states) is the furni's as the hotel defines it;
/// a hook overrides only what it says. One instance per furni, made by the plugin's container. The
/// hooks run in the room's turn: keep them quick, and never await the hotel API on this room.
/// </summary>
public abstract class FurniLogic
{
    /// <summary>The logic takes the furni: it was placed, its room loaded, or the plugin (re)loaded.</summary>
    public virtual Task OnAttachedAsync(IFurniContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>
    /// The logic lets go of the furni: it was picked up or its room unloads. What it scheduled
    /// (<see cref="IFurniContext.Schedule"/>) never runs after this.
    /// </summary>
    public virtual Task OnDetachedAsync(IFurniContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>
    /// Who may use it (double click): null keeps its definition's usage policy; a furni anyone
    /// may use, a machine every visitor plays, says <see cref="FurnitureUsageType.Everybody"/>.
    /// </summary>
    public virtual FurnitureUsageType? UsagePolicy => null;

    /// <summary>
    /// Whether an avatar may walk onto it: null keeps its definition's; true for a tile the
    /// logic acts on as it is stepped on whatever furni it is.
    /// </summary>
    public virtual bool? Walkable => null;

    /// <summary>
    /// A user used it (double click). Return true when the logic handled it; false lets the furni do
    /// what it does by default (cycle its states).
    /// </summary>
    public virtual Task<bool> OnUseAsync(
        IFurniContext ctx,
        RoomUser? user,
        int param,
        CancellationToken ct
    ) => Task.FromResult(false);

    /// <summary>A user stepped onto it.</summary>
    public virtual Task OnWalkOnAsync(IFurniContext ctx, RoomUser user, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>A user stepped off it.</summary>
    public virtual Task OnWalkOffAsync(IFurniContext ctx, RoomUser user, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>
    /// A message the plugin sent this furni (<c>IRoomGrain.InvokeFurniLogicAsync</c> or
    /// <c>IHotel.Rooms.InvokeFurniLogicAsync</c>, from a packet handler, a command, a task), its
    /// <see cref="FurniLogicMessage.Key"/> this logic's key. <paramref name="sender"/> is the
    /// message's player when they are in this room. Answer
    /// <see cref="FurniLogicReply.Handled(string?)"/> (with a payload for the sender, if any);
    /// <see cref="FurniLogicReply.NotHandled"/> by default.
    /// </summary>
    public virtual Task<FurniLogicReply> OnMessageAsync(
        IFurniContext ctx,
        FurniLogicMessage message,
        RoomUser? sender,
        CancellationToken ct
    ) => Task.FromResult(FurniLogicReply.NotHandled);
}

/// <summary>The furni a <see cref="FurniLogic"/> runs for, and its room in the room's turn.</summary>
public interface IFurniContext : IRoomTurn
{
    /// <summary>The furni as it is now.</summary>
    RoomFurni Self { get; }

    /// <summary>Sets the furni's state and shows it to the room.</summary>
    Task SetStateAsync(int state, CancellationToken ct);

    /// <summary>
    /// Fires <paramref name="evt"/> for the wired triggers of this room, with
    /// <paramref name="triggerer"/> as the triggering user when it is a player. Queued for the
    /// room's next wired tick, as the room's own events are.
    /// </summary>
    Task FireWiredAsync<TEvent>(TEvent evt, RoomUser? triggerer, CancellationToken ct)
        where TEvent : IWiredEvent;

    /// <summary>
    /// One section of the furni's extra data (its own, else its definition's) read as JSON;
    /// null when it has none or it does not read as a <typeparamref name="T"/>.
    /// </summary>
    T? ReadExtraData<T>(string section)
        where T : class;

    /// <summary>
    /// One section of the furni's extra data (its own, else its definition's) as its JSON text,
    /// whatever its shape (a number, a string, a list, an object); null when it has none.
    /// </summary>
    string? ReadExtraDataJson(string section);

    /// <summary>
    /// The other half of a linked pair (a teleporter's, or a <see cref="FurniLogicAttribute.LinkedPair"/>
    /// furni's): the item its <c>room_linker</c> extra data names; 0 when it names none.
    /// </summary>
    int PairedItemId { get; }

    /// <summary>The room <paramref name="itemId"/> stands in; null when it is in no room (an inventory).</summary>
    Task<RoomId?> GetRoomOfItemAsync(int itemId, CancellationToken ct);

    /// <summary>
    /// The number the client is sent with the furni (its object extra), 0 until set. Kept while
    /// the room is loaded: set it again in <see cref="FurniLogic.OnAttachedAsync"/> if it must last.
    /// </summary>
    int ObjectExtra { get; }

    /// <summary>Sets <see cref="ObjectExtra"/> and shows the furni again to the room.</summary>
    Task SetObjectExtraAsync(int extra, CancellationToken ct);

    /// <summary>
    /// Walks <paramref name="user"/> to the nearest of <paramref name="tiles"/> it can reach and
    /// runs <paramref name="onArrived"/> once it stands there, in the room's turn (at once when it
    /// already stands still on the nearest). False when it can reach none, or is not in the room.
    /// A later walk for the same user replaces it; a user who walks elsewhere or takes too long is
    /// forgotten, and <paramref name="onArrived"/> never runs.
    /// </summary>
    Task<bool> WalkToAsync(
        RoomUser user,
        IReadOnlyList<RoomTilePoint> tiles,
        Func<RoomUser, CancellationToken, Task> onArrived,
        CancellationToken ct
    );

    /// <summary>
    /// <see cref="WalkToAsync"/> with <paramref name="tiles"/> tried in the order given, best
    /// first, not nearest first: at once when the user stands still on the first, else walked to
    /// the first of them it can reach (a machine used from in front of it, else from beside it).
    /// </summary>
    Task<bool> WalkToInOrderAsync(
        RoomUser user,
        IReadOnlyList<RoomTilePoint> tiles,
        Func<RoomUser, CancellationToken, Task> onArrived,
        CancellationToken ct
    );

    /// <summary>The tile in front of the furni, the way it faces; null when that is off the map.</summary>
    RoomTilePoint? TileInFront { get; }

    /// <summary>
    /// The tiles touching the furni (diagonals included) <paramref name="user"/> may stand on,
    /// nearest to the user first: where it is used from when used from beside it.
    /// </summary>
    IReadOnlyList<RoomTilePoint> TilesBeside(RoomUser user);

    /// <summary>Whether the user stands still on a tile touching the furni.</summary>
    bool StandsBeside(RoomUser user);

    /// <summary>
    /// Runs <paramref name="run"/> once, <paramref name="delay"/> from now by the room's clock, in
    /// the room's turn (a spin's result, a rest's end, a game's next second). One per furni: a
    /// later call replaces the one waiting. Never runs once the furni is picked up or its room
    /// unloads.
    /// </summary>
    void Schedule(TimeSpan delay, Func<CancellationToken, Task> run);

    /// <summary>Forgets the run <see cref="Schedule"/> is waiting with, if any.</summary>
    void CancelSchedule();

    /// <summary>
    /// Shows the room the user taking one step to <paramref name="to"/> (its <c>mv</c> status, facing
    /// that way) without moving it there: the tile may be off the map, as a step through a wall
    /// into another room is. Shown once; the user stays where it stands. False when it is not in the room.
    /// </summary>
    Task<bool> ShowStepAsync(RoomUser user, RoomTilePoint to, CancellationToken ct);

    /// <summary>
    /// Sends a player to <paramref name="room"/>, with <paramref name="extension"/> for that room's
    /// arrival policies (<c>IRoomArrivalPolicy</c>) to read. It goes in as any player walking in
    /// does: bans, the door (doorbell, password), a full room and the entry policies all decide.
    /// One exception, checked by core: a <see cref="FurniLogicAttribute.LinkedPair"/> furni sending
    /// them to the room its other half (<see cref="PairedItemId"/>) stands in passes the door as a
    /// teleporter does, while bans, a full room and the entry policies still decide and the
    /// arrival policies still place them. False when the user is not a player in this room.
    /// </summary>
    Task<bool> ForwardToRoomAsync(
        RoomUser user,
        RoomId room,
        RoomEntryExtension? extension,
        CancellationToken ct
    );

    /// <summary>
    /// Lets the player watch <paramref name="room"/> from this one, as
    /// <c>IHotel.Rooms.WatchAsync</c> does (each of its packets inside
    /// <paramref name="envelope"/>, a hold on it, the same limits and ends), for this plugin. The
    /// door is the forward's (<see cref="ForwardToRoomAsync"/>): a
    /// <see cref="FurniLogicAttribute.LinkedPair"/> furni watching the room its other half stands
    /// in passes it, checked by core; bans still apply. Asked, not awaited: this runs in the
    /// room's turn. False when the user is not a player in this room.
    /// </summary>
    /// <exception cref="ArgumentException">The envelope has no carried packet field.</exception>
    Task<bool> WatchRoomAsync(
        RoomUser user,
        RoomId room,
        IPluginMessage envelope,
        TimeSpan lease,
        CancellationToken ct
    );
}
