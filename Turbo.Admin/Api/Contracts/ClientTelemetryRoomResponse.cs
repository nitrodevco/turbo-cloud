namespace Turbo.Admin.Api.Contracts;

/// <summary>One room's client health in the range: a figure per day and its raw reports, newest first.</summary>
public sealed record ClientTelemetryRoomResponse(
    int RoomId,
    string? RoomName,
    ClientHealthDay[] Days,
    ClientPerformanceRow[] Performance,
    ClientLatencyRow[] Latency,
    ClientLagRow[] Lag
);
