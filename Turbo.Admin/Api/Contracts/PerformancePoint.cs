using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The server over one stretch of time, ending at <see cref="AtUtc"/>: CPU, memory, garbage
/// collection, the thread pool, who is online, and how long rooms took to open and to reach
/// players. A timing is null when nothing was measured in it.
/// </summary>
public sealed record PerformancePoint(
    DateTime AtUtc,
    double CpuPercent,
    long WorkingSetMb,
    long HeapMb,
    int Gen2Collections,
    double GcPausePercent,
    long ThreadPoolQueue,
    int ThreadPoolThreads,
    int PlayersOnline,
    int RoomsLoaded,
    int RoomEntries,
    double? RoomEntryP50Ms,
    double? RoomEntryP95Ms,
    double? StreamDelayP95Ms
);
