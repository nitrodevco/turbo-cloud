namespace Turbo.Admin.Api.Contracts;

/// <summary>A player to create: name, motto, <c>male</c> or <c>female</c>, and a figure (empty for the default).</summary>
public sealed record CreatePlayerRequest(
    string? Name,
    string? Motto,
    string? Gender,
    string? Figure
);
