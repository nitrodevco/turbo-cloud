using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A player who holds a group directly.</summary>
public sealed record PermissionMemberView(int Id, string Name, DateTime? ExpiresAtUtc);
