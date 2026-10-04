using System;
using Orleans;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>
/// A setup link just made: its token, handed out here once and never again (the grain keeps only
/// its hash), and when it stops working.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AdminSetupLinkSnapshot
{
    [Id(0)]
    public required string Token { get; init; }

    [Id(1)]
    public required DateTime ExpiresAtUtc { get; init; }
}
