namespace Turbo.Admin.Api.Contracts;

/// <summary>A group's display name and weight; what is left out stays.</summary>
public sealed record UpdatePermissionGroupRequest(string? DisplayName, int? Weight);
