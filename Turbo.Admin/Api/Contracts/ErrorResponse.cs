namespace Turbo.Admin.Api.Contracts;

/// <summary>Why a request was refused, in words the panel can show as they are.</summary>
public sealed record ErrorResponse(string Message);
