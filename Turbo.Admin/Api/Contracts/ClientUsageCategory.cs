namespace Turbo.Admin.Api.Contracts;

/// <summary>One event category over the range.</summary>
public sealed record ClientUsageCategory(string Category, long Count, int PlayerDays);
