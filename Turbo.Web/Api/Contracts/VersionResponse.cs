namespace Turbo.Web.Api.Contracts;

/// <summary>The server's version, and the commit it was built from; null when the build stamped none.</summary>
public sealed record VersionResponse(string Version, string? Commit);
