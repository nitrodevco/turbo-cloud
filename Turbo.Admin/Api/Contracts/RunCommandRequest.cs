namespace Turbo.Admin.Api.Contracts;

/// <summary>A command line as typed in game, with or without the leading colon.</summary>
public sealed record RunCommandRequest(string Line);
