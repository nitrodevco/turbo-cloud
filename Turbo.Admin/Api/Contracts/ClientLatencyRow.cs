using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A raw latency report.</summary>
public sealed record ClientLatencyRow(
    DateTime At,
    int PlayerId,
    string? PlayerName,
    int? RoomId,
    string? RoomName,
    int AverageMs,
    int ValidAverageMs,
    int Pings
);
