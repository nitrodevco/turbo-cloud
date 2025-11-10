using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

public sealed record BuildersClubSubscriptionStatusMessageComposer : IComposer
{
    public int SecondsLeft { get; init; }
    public int FurniLimit { get; init; }
    public int MaxFurniLimit { get; init; }
    public int? SecondsLeftWithGrace { get; init; }
}
