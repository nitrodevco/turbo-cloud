using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A raw performance report.</summary>
public sealed record ClientPerformanceRow(
    DateTime At,
    int PlayerId,
    string? PlayerName,
    int? RoomId,
    string? RoomName,
    string Browser,
    string Os,
    string Device,
    string UserAgent,
    int Fps,
    int MemoryMb,
    int SlowUpdates,
    int GarbageCollections
);
