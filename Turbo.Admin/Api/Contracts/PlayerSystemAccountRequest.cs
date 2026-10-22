namespace Turbo.Admin.Api.Contracts;

/// <summary>Whether a player is a system or service account.</summary>
public sealed record PlayerSystemAccountRequest(bool SystemAccount);
