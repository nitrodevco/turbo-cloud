using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The group a staff member is previewing: until <paramref name="EndsAtUtc"/>, or until they stop,
/// the panel and the hotel treat them as a plain member of it.
/// </summary>
public sealed record PermissionPreviewView(string Group, string DisplayName, DateTime EndsAtUtc);
