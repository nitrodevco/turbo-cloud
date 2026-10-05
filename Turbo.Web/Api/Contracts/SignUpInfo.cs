namespace Turbo.Web.Api.Contracts;

/// <summary>Someone signed in with Discord with no player yet: their Discord name and the hotel name offered.</summary>
public sealed record SignUpInfo(string DiscordName, string SuggestedName);
