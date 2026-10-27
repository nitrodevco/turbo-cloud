using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Contracts.Rooms;

/// <summary>What a user in a room is: a player, a pet or a bot.</summary>
public enum RoomUserKind
{
    Player,
    Pet,
    Bot,
}

/// <summary>
/// A user in a room, as a plugin's furni logic or wired box sees one, where it stands as it is
/// asked. <see cref="Index"/> is the room's address for it (what the client and wired name it by);
/// <see cref="Player"/> is set for a player only. <see cref="IsIdle"/> is the client's sleeping
/// avatar; <see cref="EffectId"/> the effect it wears, 0 for none.
/// </summary>
public sealed record RoomUser(
    int Index,
    RoomUserKind Kind,
    PlayerId? Player,
    string Name,
    int X,
    int Y,
    Rotation Rotation,
    bool IsWalking,
    bool IsIdle,
    int EffectId
);

/// <summary>
/// A floor furni in a room: its id, its definition's logic name, where it stands and which way it
/// faces, its state, and its definition (its name, states, size, custom params and what an avatar
/// may do on it).
/// </summary>
public sealed record RoomFurni(
    int Id,
    string Logic,
    int X,
    int Y,
    double Z,
    int State,
    Rotation Rotation,
    FurnitureDefinitionSnapshot Definition
);
