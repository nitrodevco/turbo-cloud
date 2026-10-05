namespace Turbo.Web.Discord;

/// <summary>Who signed in with Discord: their id, their username, and the name they show, if any.</summary>
public sealed record DiscordUser(string Id, string Username, string? GlobalName);
