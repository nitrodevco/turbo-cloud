namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A client config to take the external variables from: one JSON object, as
/// <c>nitro-config.json</c>; and whether the hotel's variables it lacks are removed.
/// </summary>
public sealed record VariableImportRequest(string? Json, bool RemoveMissing = false);
