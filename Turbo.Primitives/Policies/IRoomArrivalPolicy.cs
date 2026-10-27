using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Policies;

/// <summary>
/// A policy over where a player who has been let into a room appears in it: the tile and the way
/// they face instead of the door, and a tile to walk to from there. Asked as the room makes the
/// player's avatar, after every entry check (bans, the door, its doorbell and password, a full
/// room, <see cref="IRoomEntryPolicy"/>) has let them in: it never decides whether they come in,
/// only where. A public class implementing it is found in a plugin's assembly as it loads and
/// taken away as it unloads; see <see cref="IGamePolicies"/> for the order several run in, each on
/// what the one before decided. A player arriving inside a teleporter is placed by the teleporter;
/// the policies are not asked.
/// <para>
/// It runs on the room's turn: answer from the request (<see cref="RoomArrivalRequest.Extension"/>
/// carries what the plugin's furni sent with the player) and from memory, and never ask this room
/// anything. A start tile the room cannot stand the player on (off the map, a wall, a blocked
/// tile) is not used: they appear at the door. A policy that throws is logged and passed over.
/// </para>
/// </summary>
public interface IRoomArrivalPolicy
{
    /// <summary>Lower runs first. 0 by default.</summary>
    int Order => 0;

    /// <param name="proposed">
    /// What the policies before this one decided; the door at the start. Return it to let it
    /// stand, <c>proposed.StartAt(x, y, rotation)</c> or <c>proposed with { ... }</c> to change it.
    /// </param>
    ValueTask<RoomArrivalDecision> DecideAsync(
        RoomArrivalRequest request,
        RoomArrivalDecision proposed,
        CancellationToken ct
    );
}

/// <summary>
/// A player arriving in a room: how they came (<paramref name="Method"/>), the room that sent them
/// (zero when none did) and what a plugin's furni sent with them (null when nothing did).
/// </summary>
public sealed record RoomArrivalRequest(
    PlayerId PlayerId,
    RoomId RoomId,
    RoomEntryMethodType Method,
    RoomId SourceRoomId,
    RoomEntryExtension? Extension
);

/// <summary>
/// Where an arriving player appears. With <see cref="SkipDoor"/> false they appear on the room's
/// door as always (<see cref="X"/>, <see cref="Y"/> and <see cref="Rotation"/> are the door's);
/// with it true they appear on that tile, facing that way, without standing on the door first.
/// <see cref="WalkTo"/>, when set, is where they walk once they are in.
/// </summary>
public sealed record RoomArrivalDecision
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required Rotation Rotation { get; init; }

    public bool SkipDoor { get; init; }

    public RoomTilePoint? WalkTo { get; init; }

    /// <summary>The door: where a player appears unless a policy says otherwise.</summary>
    public static RoomArrivalDecision Door(int x, int y, Rotation rotation) =>
        new()
        {
            X = x,
            Y = y,
            Rotation = rotation,
        };

    /// <summary>Appears on (<paramref name="x"/>, <paramref name="y"/>) facing <paramref name="rotation"/>, not at the door.</summary>
    public RoomArrivalDecision StartAt(int x, int y, Rotation rotation) =>
        this with
        {
            X = x,
            Y = y,
            Rotation = rotation,
            SkipDoor = true,
        };
}
