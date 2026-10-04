using System.Text.Json;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Finishing a setup link: the passkey just made, and what to call it.</summary>
public sealed record SetupCompleteRequest(
    string Token,
    string CeremonyId,
    JsonElement Credential,
    string? PasskeyName
);
