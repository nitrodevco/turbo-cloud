using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One day's events, and the players who logged them.</summary>
public sealed record ClientUsageDay(DateOnly Day, long Events, int Players);
