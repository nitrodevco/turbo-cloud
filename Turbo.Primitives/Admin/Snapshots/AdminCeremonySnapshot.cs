using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>
/// A passkey ceremony in progress: whose it is, and the options the browser was given, which the
/// answer has to be checked against (they hold the challenge).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AdminCeremonySnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required string OptionsJson { get; init; }
}
