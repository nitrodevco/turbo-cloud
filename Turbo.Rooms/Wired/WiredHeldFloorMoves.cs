using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The furni moves of one run of a stack's effects, held while they run and told to the room
/// once they have, one move per furni (official client capture, WIN63-202610201032: two move and
/// two rotate effects on one sofa sent one WiredMovements per firing, never one per effect).
/// Without "Execute In Order" the room sees the effects' moves of a furni as one, from where it
/// stood to where they leave it, facing the way they leave it. With it, the room sees only the
/// last move, from where the move before it left the furni. A furni left on the tile it stood on
/// was only turned: that is a plain update of it (<see cref="Turned"/>), with or without the
/// add-on (official client capture, 2026-10-22T22-02-10: one rotate effect 6/6, and two rotates
/// picked by the random add-on 1/1).
/// </summary>
internal sealed class WiredHeldFloorMoves(bool inOrder)
{
    private readonly List<WiredFloorItemMovementSnapshot> _moves = [];

    /// <summary>The moves to send, one per furni, in the order the furni first moved.</summary>
    public IEnumerable<WiredFloorItemMovementSnapshot> Moves
    {
        get
        {
            foreach (var move in _moves)
            {
                if (!IsTurnOnly(move))
                    yield return move;
            }
        }
    }

    /// <summary>The furni the moves left on their own tile, only turned.</summary>
    public IEnumerable<RoomObjectId> Turned
    {
        get
        {
            foreach (var move in _moves)
            {
                if (IsTurnOnly(move))
                    yield return move.ObjectId;
            }
        }
    }

    public bool IsEmpty => _moves.Count == 0;

    /// <summary>Takes an effect's moves in, leaving its list empty.</summary>
    public void Hold(List<WiredFloorItemMovementSnapshot> moves)
    {
        foreach (var move in moves)
        {
            var index = _moves.FindIndex(x => x.ObjectId == move.ObjectId);

            if (inOrder)
            {
                if (index >= 0)
                    _moves.RemoveAt(index);

                _moves.Add(move);

                continue;
            }

            if (index < 0)
            {
                _moves.Add(move);

                continue;
            }

            var held = _moves[index];

            _moves[index] = move with
            {
                SourceX = held.SourceX,
                SourceY = held.SourceY,
                SourceZ = held.SourceZ,
            };
        }

        moves.Clear();
    }

    public void Clear() => _moves.Clear();

    private static bool IsTurnOnly(WiredFloorItemMovementSnapshot move) =>
        move.SourceX == move.TargetX
        && move.SourceY == move.TargetY
        && move.SourceZ == move.TargetZ;
}
