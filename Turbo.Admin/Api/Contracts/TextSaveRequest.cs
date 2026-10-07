namespace Turbo.Admin.Api.Contracts;

/// <summary>A text to set: its key, and its value as the file writes it (<c>\n</c> for a line break).</summary>
public sealed record TextSaveRequest(string? Key, string? Value);
