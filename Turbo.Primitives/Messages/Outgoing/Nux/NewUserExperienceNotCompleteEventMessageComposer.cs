using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Nux;

[GenerateSerializer, Immutable]
public sealed record NewUserExperienceNotCompleteEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
