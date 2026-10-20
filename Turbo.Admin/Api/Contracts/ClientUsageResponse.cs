using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>The Usage page over a range of days (UTC): events per day, the most logged events and the categories.</summary>
public sealed record ClientUsageResponse(
    DateOnly From,
    DateOnly To,
    int RawRetentionDays,
    ClientUsageDay[] Days,
    ClientUsageEvent[] Top,
    ClientUsageCategory[] Categories
);
