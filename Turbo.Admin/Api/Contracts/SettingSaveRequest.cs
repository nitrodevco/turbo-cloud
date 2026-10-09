namespace Turbo.Admin.Api.Contracts;

/// <summary>A server setting to override: its path, and its value as JSON (<c>"text"</c>, <c>true</c>, <c>120</c>).</summary>
public sealed record SettingSaveRequest(string? Path, string? Value);
