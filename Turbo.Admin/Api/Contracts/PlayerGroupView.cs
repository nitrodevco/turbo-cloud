using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A group a player holds directly.</summary>
public sealed record PlayerGroupView(
    string Name,
    string DisplayName,
    int Weight,
    DateTime? ExpiresAtUtc
);
