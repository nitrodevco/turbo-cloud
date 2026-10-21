namespace Turbo.Admin.Api.Contracts;

/// <summary>How clients ran in one room over the range: chat lag, frame rate and latency.</summary>
public sealed record ClientHealthRoom(
    int RoomId,
    string? RoomName,
    long LagWarnings,
    int LagReports,
    int FpsSamples,
    int FpsP50,
    int FpsP95,
    int LatencyReports,
    int LatencyP50Ms,
    int PlayerDays
);
