namespace Turbo.Web.Api.Contracts;

/// <summary>Whether a hotel name can be had, and why not when it can't.</summary>
public sealed record NameCheckResponse(bool Available, string? Reason);
