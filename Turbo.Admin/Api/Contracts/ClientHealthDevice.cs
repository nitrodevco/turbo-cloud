namespace Turbo.Admin.Api.Contracts;

/// <summary>How clients of one browser, system and kind of device ran over the range. Player days count a player once a day.</summary>
public sealed record ClientHealthDevice(
    string Browser,
    string Os,
    string Device,
    int Reports,
    int PlayerDays,
    int FpsP50,
    int FpsP95,
    int MemoryP50Mb,
    int MemoryP95Mb,
    int LatencyReports,
    int LatencyP50Ms,
    int LatencyP95Ms,
    long SlowUpdates
);
