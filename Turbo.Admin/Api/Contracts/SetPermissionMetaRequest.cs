namespace Turbo.Admin.Api.Contracts;

/// <summary>A meta value to set; a duration makes it temporary.</summary>
public sealed record SetPermissionMetaRequest(
    string? Key,
    string? Value,
    string? Duration,
    bool Extend
);
