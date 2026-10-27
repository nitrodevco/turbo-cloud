using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Contracts.Rooms;

/// <summary>
/// A room as a plugin acts on it inside the room's own turn (a furni logic's hook, a scheduled
/// run): its users and floor, and what it can do to them through the room's own rules. Everything
/// here is the room's own and in memory; never await the hotel API on this room from here, the
/// room would wait on itself. Each user action answers false when the user is not in the room (or
/// is not of the kind it acts on) and does nothing then.
/// </summary>
public interface IRoomTurn
{
    RoomId Room { get; }

    /// <summary>Everyone in the room: players, pets and bots.</summary>
    IReadOnlyList<RoomUser> RoomUsers { get; }

    /// <summary>The room's floor: its tiles and what stands on them.</summary>
    IRoomMap Map { get; }

    /// <summary>
    /// A chat bubble over <paramref name="speaker"/> (a player or a bot), shown as
    /// <paramref name="speech"/> says. A pet is not made to speak, and only a player can be
    /// whispered over (<see cref="RoomSpeech.OnlyForThem"/>): false for both, and for no text.
    /// </summary>
    Task<bool> SayAsync(RoomUser speaker, string text, RoomSpeech speech, CancellationToken ct);

    /// <summary>Puts the effect on the user (0 takes it off), shown to the room.</summary>
    Task<bool> SetEffectAsync(RoomUser user, int effectId, CancellationToken ct);

    /// <summary>Stops the user where it is, finishing a step under way.</summary>
    Task<bool> StopWalkingAsync(RoomUser user, CancellationToken ct);

    /// <summary>Turns the user (body and head) to <paramref name="rotation"/>.</summary>
    Task<bool> SetRotationAsync(RoomUser user, Rotation rotation, CancellationToken ct);

    /// <summary>Dresses a bot in <paramref name="figure"/> (its gender kept), shown to the room and kept as the bot's.</summary>
    Task<bool> SetBotFigureAsync(RoomUser bot, string figure, CancellationToken ct);

    /// <summary>
    /// Has a bot follow <paramref name="target"/> around the room, as its owner's "follow me" does;
    /// null stops it following.
    /// </summary>
    Task<bool> SetBotFollowingAsync(RoomUser bot, RoomUser? target, CancellationToken ct);

    /// <summary>
    /// Drops a temporary floor furni of <paramref name="definition"/> on the tile (on top of what
    /// stands there), shown to the room: it is never saved, nobody can pick it up, and it goes when
    /// removed (<see cref="RemoveTemporaryFurniAsync"/>) or when the room unloads. The definition
    /// may be a hotel's one changed, a <c>with</c> another logic name, so the dropped furni runs
    /// another logic than its kind sold does. Owned by the owner of the furni asking. Null when
    /// it cannot stand there.
    /// </summary>
    Task<RoomFurni?> PlaceTemporaryFurniAsync(
        FurnitureDefinitionSnapshot definition,
        RoomTilePoint tile,
        Rotation rotation,
        CancellationToken ct
    );

    /// <summary>Takes a temporary furni (<see cref="PlaceTemporaryFurniAsync"/>) out of the room; false for any other.</summary>
    Task<bool> RemoveTemporaryFurniAsync(int furniId, CancellationToken ct);
}
