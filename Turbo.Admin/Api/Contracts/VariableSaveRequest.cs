namespace Turbo.Admin.Api.Contracts;

/// <summary>An external variable to set: its key, and its value as JSON (<c>"text"</c>, <c>true</c>, <c>120</c>).</summary>
public sealed record VariableSaveRequest(string? Key, string? Value);
