namespace Turbo.Admin.Api.Contracts;

/// <summary>A new group.</summary>
public sealed record CreatePermissionGroupRequest(string? Name, string? DisplayName, int Weight);
