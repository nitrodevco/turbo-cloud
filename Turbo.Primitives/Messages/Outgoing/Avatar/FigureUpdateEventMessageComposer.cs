using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Avatar;

[GenerateSerializer, Immutable]
public sealed record FigureUpdateEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
