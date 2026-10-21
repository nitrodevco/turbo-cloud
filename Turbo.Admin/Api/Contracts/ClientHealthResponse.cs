using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>The Client health page over a range of days (UTC): a figure per day, per browser and device, per room, and the login steps. Percentiles over several days weight each day's by its reports.</summary>
public sealed record ClientHealthResponse(
    DateOnly From,
    DateOnly To,
    int RawRetentionDays,
    ClientHealthDay[] Days,
    ClientHealthDevice[] Devices,
    ClientHealthRoom[] Rooms,
    ClientLoginStepFigure[] Login,
    ClientTelemetryFilters Filters
);
