using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A new panel session. The token goes in the <c>Authorization: Bearer</c> header.</summary>
public sealed record SessionResponse(
    string SessionToken,
    DateTime ExpiresAtUtc,
    AdminPlayer Player
);
