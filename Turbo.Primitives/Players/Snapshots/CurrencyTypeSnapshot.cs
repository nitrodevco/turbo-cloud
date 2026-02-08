using Orleans;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Primitives.Players.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CurrencyTypeSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required CurrencyType CurrencyType { get; init; }

    [Id(3)]
    public int? ActivityPointType { get; init; }

    [Id(4)]
    public required bool Enabled { get; init; }
}
