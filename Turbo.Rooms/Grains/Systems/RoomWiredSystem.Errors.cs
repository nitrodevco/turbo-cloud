using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private readonly Dictionary<string, WiredErrorLogEntry> _errorLogsByKey = [];
    private int _nextErrorId = 1;

    /// <summary>
    /// Records a wired fault for the monitor tab. Faults are aggregated by name and category;
    /// once the configured cap is reached the least recently seen entry makes room.
    /// </summary>
    internal void RecordError(string errorName, string category, long now)
    {
        var key = $"{errorName}|{category}";

        if (_errorLogsByKey.TryGetValue(key, out var entry))
        {
            entry.ThrowCount++;
            entry.LastOccurrenceMs = now;

            return;
        }

        var cap = _roomGrain._wiredConfig.MaxErrorLogEntries;

        if (cap <= 0)
            return;

        if (_errorLogsByKey.Count >= cap)
        {
            var oldest = _errorLogsByKey.MinBy(x => x.Value.LastOccurrenceMs);

            _errorLogsByKey.Remove(oldest.Key);
        }

        _errorLogsByKey[key] = new WiredErrorLogEntry
        {
            Id = _nextErrorId++,
            ErrorName = errorName,
            Category = category,
            ThrowCount = 1,
            LastOccurrenceMs = now,
        };
    }

    internal static string GetErrorCategory(IWiredBox box) => $"{box.WiredType} {box.WiredCode}";

    public ImmutableArray<WiredErrorLogSnapshot> GetErrorLogs(long now) =>
        _errorLogsByKey
            .Values.OrderByDescending(x => x.LastOccurrenceMs)
            .Select(x => new WiredErrorLogSnapshot
            {
                ErrorId = x.Id,
                ErrorName = x.ErrorName,
                Category = x.Category,
                ThrowCount = x.ThrowCount,
                MsSinceLastOccurrence = now - x.LastOccurrenceMs,
            })
            .ToImmutableArray();

    public void ClearErrorLogs() => _errorLogsByKey.Clear();
}
