namespace Turbo.Admin.Api.Contracts;

/// <summary>A client config to take the external variables from: one JSON object, as <c>nitro-config.json</c>.</summary>
public sealed record VariableImportRequest(string? Json);
