using System;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private readonly int[] _errorCounts = new int[WiredErrors.All.Length];
    private readonly long[] _errorLastMs = new long[WiredErrors.All.Length];

    /// <summary>Counts one occurrence of a monitor error.</summary>
    internal void RecordError(WiredErrorKind kind, long now)
    {
        _errorCounts[(int)kind]++;
        _errorLastMs[(int)kind] = now;
    }

    /// <summary>
    /// All ten monitor errors in their fixed order; one that never happened has a count of 0 and
    /// -1 for the time since it last did.
    /// </summary>
    public ImmutableArray<WiredErrorLogSnapshot> GetErrorLogs(long now) =>
        WiredErrors
            .All.Select(
                (error, i) =>
                    new WiredErrorLogSnapshot
                    {
                        ErrorId = error.Id,
                        ErrorName = error.Name,
                        Category = error.Category,
                        ThrowCount = _errorCounts[i],
                        MsSinceLastOccurrence = _errorCounts[i] > 0 ? now - _errorLastMs[i] : -1,
                    }
            )
            .ToImmutableArray();

    public void ClearErrorLogs()
    {
        Array.Clear(_errorCounts);
        Array.Clear(_errorLastMs);
    }
}
