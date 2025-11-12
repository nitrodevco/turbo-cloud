using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Help;

[GenerateSerializer, Immutable]
public sealed record GuideSessionMessageMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
