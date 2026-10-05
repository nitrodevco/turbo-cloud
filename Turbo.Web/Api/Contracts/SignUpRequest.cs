namespace Turbo.Web.Api.Contracts;

/// <summary>The hotel name chosen on signing up, and <c>male</c> or <c>female</c>.</summary>
public sealed record SignUpRequest(string? Name, string? Gender);
