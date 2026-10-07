using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// How this server has been running over the last <see cref="Hours"/>: its figures over time,
/// oldest first, and how long each room operation took. Kept in memory since the server started
/// (<see cref="RecordingSinceUtc"/>), so a restart starts it again.
/// </summary>
public sealed record PerformanceResponse(
    int Hours,
    int SampleSeconds,
    int PointSeconds,
    DateTime? RecordingSinceUtc,
    IReadOnlyList<PerformancePoint> Points,
    IReadOnlyList<PerformanceStage> Stages
);
