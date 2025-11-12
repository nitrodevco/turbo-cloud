using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

[GenerateSerializer, Immutable]
public sealed record QuestionAnsweredEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
