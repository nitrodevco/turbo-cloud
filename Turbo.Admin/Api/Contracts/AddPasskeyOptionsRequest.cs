using System.Text.Json;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Asking to add a passkey: a fresh answer from one the player already has, so a session left
/// open somewhere cannot add a way in.
/// </summary>
public sealed record AddPasskeyOptionsRequest(string CeremonyId, JsonElement Credential);
