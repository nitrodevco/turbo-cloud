using System.Text.Json;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A browser's answer to a passkey prompt: the ceremony it was for, and the credential as
/// <c>PublicKeyCredential.toJSON()</c> gives it.
/// </summary>
public sealed record PasskeyAnswerRequest(string CeremonyId, JsonElement Credential);
