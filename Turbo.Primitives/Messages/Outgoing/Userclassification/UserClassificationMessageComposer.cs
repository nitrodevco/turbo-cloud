using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Userclassification;

[GenerateSerializer, Immutable]
public sealed record UserClassificationMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
