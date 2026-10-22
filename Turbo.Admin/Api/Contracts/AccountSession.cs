using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A panel session, named by its id; <paramref name="Current"/> is the caller's own.</summary>
public sealed record AccountSession(
    int Id,
    DateTime SignedInAtUtc,
    DateTime ExpiresAtUtc,
    bool Current
);
