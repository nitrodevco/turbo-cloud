namespace Turbo.Admin.Api.Contracts;

/// <summary>A group to put a player in; a duration makes the membership temporary.</summary>
public sealed record AddPermissionGroupMemberRequest(string? Group, string? Duration, bool Extend);
