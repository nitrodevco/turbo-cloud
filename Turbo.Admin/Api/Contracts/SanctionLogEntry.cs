using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A sanction in the sanctions log: on whom, what (a ban or a caution), why, who gave it and when,
/// until when, whether it was lifted and by whom, and whether it holds now. A name is null for a
/// player who is gone; an issuer id is null for the console.
/// </summary>
public sealed record SanctionLogEntry(
    int Id,
    DateTime IssuedUtc,
    int PlayerId,
    string? PlayerName,
    string Kind,
    string Reason,
    int? IssuerId,
    string? IssuerName,
    DateTime? ExpiresUtc,
    DateTime? RevokedUtc,
    int? RevokedById,
    string? RevokedByName,
    bool IsActive
);
