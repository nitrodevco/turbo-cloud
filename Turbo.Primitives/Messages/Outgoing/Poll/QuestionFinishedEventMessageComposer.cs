using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

[GenerateSerializer, Immutable]
public sealed record QuestionFinishedEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
