using Orleans;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Movements;

/// <summary>
/// The asset a movement moved: a furni, pet or bot by its id and definition, or a currency (id
/// and definition 0) by its <see cref="Currency"/>, which only a currency has.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct AssetRef(
    [property: Id(0)] AssetKind Kind,
    [property: Id(1)] long Id,
    [property: Id(2)] int DefinitionId,
    [property: Id(3)] CurrencyKind? Currency
);
