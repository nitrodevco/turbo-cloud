namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// An external variable to link to a server setting by its path, or to a gamedata file's address
/// (<c>furnidata_json</c>); neither unlinks it.
/// </summary>
public sealed record VariableLinkRequest(string? Key, string? Setting, string? File);
