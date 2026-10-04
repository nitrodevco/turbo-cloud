namespace Turbo.Admin.Api.Contracts;

/// <summary>Whose setup link it is, and whether finishing it replaces passkeys they already have.</summary>
public sealed record SetupInfoResponse(string PlayerName, bool ReplacesExisting);
