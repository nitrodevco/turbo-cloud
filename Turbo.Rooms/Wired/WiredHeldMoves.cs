using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The movements of one run of a stack's effects, held while they run and told to the room once
/// they have, in one WiredMovements, one move per furni, wall item and user (official client
/// capture, WIN63-202610201032: two move and two rotate effects on one sofa sent one
/// WiredMovements per firing, never one per effect; 2026-10-03: a walk tile moved with the user
/// it carried, both in one packet). Without "Execute In Order" the room sees the effects' moves
/// of a furni as one, from where it stood to where they leave it, facing the way they leave it.
/// With it, the room sees only the last move, from where the move before it left the furni. A
/// furni left on the tile it stood on was only turned: that is a plain update of it
/// (<see cref="Turned"/>), with or without the add-on (official client capture,
/// 2026-10-22T22-02-10: one rotate effect 6/6, and two rotates picked by the random add-on 1/1).
/// Wall items and users are held the same way as furni (inferred: a carried user's move has to
/// match its furni's); a user's turns, the last one each.
/// </summary>
internal sealed class WiredHeldMoves(bool inOrder)
{
    private readonly List<WiredFloorItemMovementSnapshot> _floorMoves = [];
    private readonly List<WiredWallItemMovementSnapshot> _wallMoves = [];
    private readonly List<WiredUserMovementSnapshot> _userMoves = [];
    private readonly List<WiredUserDirectionSnapshot> _userDirections = [];

    /// <summary>The furni moves to send, one per furni, in the order the furni first moved.</summary>
    public IEnumerable<WiredFloorItemMovementSnapshot> FloorMoves
    {
        get
        {
            foreach (var move in _floorMoves)
            {
                if (!IsTurnOnly(move))
                    yield return move;
            }
        }
    }

    public IReadOnlyList<WiredWallItemMovementSnapshot> WallMoves => _wallMoves;

    public IReadOnlyList<WiredUserMovementSnapshot> UserMoves => _userMoves;

    public IReadOnlyList<WiredUserDirectionSnapshot> UserDirections => _userDirections;

    /// <summary>The furni the moves left on their own tile, only turned.</summary>
    public IEnumerable<RoomObjectId> Turned
    {
        get
        {
            foreach (var move in _floorMoves)
            {
                if (IsTurnOnly(move))
                    yield return move.ObjectId;
            }
        }
    }

    public bool IsEmpty =>
        _floorMoves.Count == 0
        && _wallMoves.Count == 0
        && _userMoves.Count == 0
        && _userDirections.Count == 0;

    /// <summary>Takes an effect's movements in, leaving its lists empty.</summary>
    public void Hold(
        List<WiredFloorItemMovementSnapshot> floorMoves,
        List<WiredWallItemMovementSnapshot> wallMoves,
        List<WiredUserMovementSnapshot> userMoves,
        List<WiredUserDirectionSnapshot> userDirections
    )
    {
        foreach (var move in floorMoves)
            Hold(
                _floorMoves,
                move,
                static x => x.ObjectId,
                static (move, held) =>
                    move with
                    {
                        SourceX = held.SourceX,
                        SourceY = held.SourceY,
                        SourceZ = held.SourceZ,
                    }
            );

        foreach (var move in wallMoves)
            Hold(
                _wallMoves,
                move,
                static x => x.ObjectId,
                static (move, held) =>
                    move with
                    {
                        SourceX = held.SourceX,
                        SourceY = held.SourceY,
                        SourceOffsetX = held.SourceOffsetX,
                        SourceOffsetY = held.SourceOffsetY,
                    }
            );

        foreach (var move in userMoves)
            Hold(
                _userMoves,
                move,
                static x => x.ObjectId,
                static (move, held) =>
                    move with
                    {
                        SourceX = held.SourceX,
                        SourceY = held.SourceY,
                        SourceZ = held.SourceZ,
                    }
            );

        foreach (var direction in userDirections)
        {
            var index = _userDirections.FindIndex(x => x.ObjectId == direction.ObjectId);

            if (index >= 0)
                _userDirections[index] = direction;
            else
                _userDirections.Add(direction);
        }

        floorMoves.Clear();
        wallMoves.Clear();
        userMoves.Clear();
        userDirections.Clear();
    }

    public void Clear()
    {
        _floorMoves.Clear();
        _wallMoves.Clear();
        _userMoves.Clear();
        _userDirections.Clear();
    }

    /// <summary>
    /// Holds <paramref name="move"/>: in order, in place of the object's earlier move; else as
    /// the earlier move carried on to where this one leaves it (<paramref name="fromHeld"/>).
    /// </summary>
    private void Hold<T>(
        List<T> held,
        T move,
        Func<T, RoomObjectId> objectIdOf,
        Func<T, T, T> fromHeld
    )
    {
        var objectId = objectIdOf(move);
        var index = held.FindIndex(x => objectIdOf(x) == objectId);

        if (inOrder)
        {
            if (index >= 0)
                held.RemoveAt(index);

            held.Add(move);

            return;
        }

        if (index < 0)
        {
            held.Add(move);

            return;
        }

        held[index] = fromHeld(move, held[index]);
    }

    private static bool IsTurnOnly(WiredFloorItemMovementSnapshot move) =>
        move.SourceX == move.TargetX
        && move.SourceY == move.TargetY
        && move.SourceZ == move.TargetZ;
}
