namespace Turbo.Web.Api.Contracts;

/// <summary>The client's address with a fresh login ticket, for the site to load.</summary>
public sealed record PlayResponse(string ClientUrl);
