namespace Turbo.Admin.Api.Contracts;

/// <summary>What a change did: <c>Changed</c> false when it was already so.</summary>
public sealed record PermissionChangeResponse(bool Changed, string Message);
