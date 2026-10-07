namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One measured operation (a room entry stage, a room load step, a chat command, room updates
/// reaching players) over the range: how often, and how long it took. The percentiles come from a
/// sample of the timings when there were many.
/// </summary>
public sealed record PerformanceStage(
    string Stage,
    int Count,
    double P50Ms,
    double P95Ms,
    double MaxMs
);
