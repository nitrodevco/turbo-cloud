using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A sanction on a player: whether it still holds, why, and who gave and lifted it.</summary>
public sealed record PlayerSanctionItem(
    string Kind,
    string Reason,
    string? IssuerName,
    DateTime IssuedUtc,
    DateTime? ExpiresUtc,
    DateTime? RevokedUtc,
    string? RevokedByName,
    bool IsActive
);
