namespace Turbo.Admin.Api.Contracts;

/// <summary>One event (category, type, action) over the range: how often, and by how many player days.</summary>
public sealed record ClientUsageEvent(
    string Category,
    string Type,
    string Action,
    long Count,
    int PlayerDays
);
