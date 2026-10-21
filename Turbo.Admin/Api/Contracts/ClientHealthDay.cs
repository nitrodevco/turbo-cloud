using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One day of client health: frame rate, memory and latency percentiles, and chat lag. Zero where nothing was reported.</summary>
public sealed record ClientHealthDay(
    DateOnly Day,
    int Reports,
    int Players,
    int FpsP50,
    int FpsP95,
    int MemoryP50Mb,
    int MemoryP95Mb,
    int LatencyReports,
    int LatencyP50Ms,
    int LatencyP95Ms,
    long LagWarnings
);
