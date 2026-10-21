namespace Turbo.Admin.Api.Contracts;

/// <summary>One player's raw client reports in the range, newest first.</summary>
public sealed record ClientTelemetryPlayerResponse(
    int PlayerId,
    string? PlayerName,
    ClientPerformanceRow[] Performance,
    ClientLatencyRow[] Latency,
    ClientLagRow[] Lag,
    ClientEventRow[] Events,
    ClientLoginRow[] Logins
);
