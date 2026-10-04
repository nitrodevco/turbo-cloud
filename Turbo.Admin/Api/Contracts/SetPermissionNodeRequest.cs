namespace Turbo.Admin.Api.Contracts;

/// <summary>A node or wildcard to grant or deny; a duration (30m, 12h, 7d, 2w) makes it temporary, and <c>Extend</c> adds it to one running.</summary>
public sealed record SetPermissionNodeRequest(
    string? Node,
    bool Value,
    string? Duration,
    bool Extend
);
