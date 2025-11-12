using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record BuildersClubSubscriptionStatusMessageComposer : IComposer
{
    [Id(0)]
    public int SecondsLeft { get; init; }

    [Id(1)]
    public int FurniLimit { get; init; }

    [Id(2)]
    public int MaxFurniLimit { get; init; }

    [Id(3)]
    public int? SecondsLeftWithGrace { get; init; }
}
