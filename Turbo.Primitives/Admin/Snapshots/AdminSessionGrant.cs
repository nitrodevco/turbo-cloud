using Orleans;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>
/// A session just opened by a passkey. The token is handed out here once and never again:
/// the grain keeps only its hash.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AdminSessionGrant
{
    [Id(0)]
    public required string SessionToken { get; init; }

    [Id(1)]
    public required AdminSessionSnapshot Session { get; init; }
}
