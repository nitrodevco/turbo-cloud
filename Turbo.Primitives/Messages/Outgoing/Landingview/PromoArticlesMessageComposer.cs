using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Landingview;

[GenerateSerializer, Immutable]
public sealed record PromoArticlesMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
