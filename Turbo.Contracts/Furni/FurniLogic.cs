using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms;

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
}

/// <summary>The furni a <see cref="FurniLogic"/> runs for, and its room.</summary>
public interface IFurniContext
{
    RoomId Room { get; }

    /// <summary>The furni as it is now.</summary>
    RoomFurni Self { get; }

    /// <summary>Everyone in the room: players, pets and bots.</summary>
    IReadOnlyList<RoomUser> RoomUsers { get; }

    /// <summary>Sets the furni's state and shows it to the room.</summary>
    Task SetStateAsync(int state, CancellationToken ct);

    /// <summary>
    /// Fires <paramref name="evt"/> for the wired triggers of this room, with
    /// <paramref name="triggerer"/> as the triggering user when it is a player. Queued for the
    /// room's next wired tick, as the room's own events are.
    /// </summary>
    Task FireWiredAsync<TEvent>(TEvent evt, RoomUser? triggerer, CancellationToken ct)
        where TEvent : IWiredEvent;
}
