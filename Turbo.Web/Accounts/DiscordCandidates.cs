namespace Turbo.Web.Accounts;

/// <summary>The names a Discord account offers a hotel name from: its username, then the name it shows.</summary>
public sealed record DiscordCandidates(string Username, string? GlobalName);
