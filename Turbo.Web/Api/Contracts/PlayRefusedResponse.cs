namespace Turbo.Web.Api.Contracts;

/// <summary>
/// Play refused by one of the hotel's play policies: why, in words for the person, and where to
/// send them (a page to visit first), or null.
/// </summary>
public sealed record PlayRefusedResponse(string Message, string? RedirectUrl);
