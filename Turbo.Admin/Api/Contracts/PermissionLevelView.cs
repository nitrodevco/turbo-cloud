using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>The security level the client is sent, what set it, and what it makes the client offer that the server refuses.</summary>
public sealed record PermissionLevelView(
    string Level,
    int Value,
    string? Source,
    ImmutableArray<PermissionLevelNode> ShownButRefused
);
