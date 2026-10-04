using System.Text.Json;

namespace Turbo.Admin.Api.Contracts;

public sealed record AddPasskeyRequest(string CeremonyId, JsonElement Credential, string? Name);
