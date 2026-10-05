using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The Discord account a player signs in to the public site with: its id and username, when it was
/// linked, and how many sign-ins to the site are live.
/// </summary>
public sealed record PlayerDiscordInfo(
    string Id,
    string Username,
    DateTime LinkedAtUtc,
    int ActiveSignIns
);
