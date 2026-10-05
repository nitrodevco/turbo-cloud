using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A player in the panel's search results, with their Discord username when they sign in with Discord.</summary>
public sealed record PlayerListItem(
    int Id,
    string Name,
    string? Motto,
    bool IsOnline,
    DateTime? LastLoginUtc,
    DateTime JoinedUtc,
    int RoomsOwned,
    string? DiscordUsername
);
