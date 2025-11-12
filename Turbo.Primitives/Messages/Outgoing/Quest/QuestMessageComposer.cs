using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

[GenerateSerializer, Immutable]
public sealed record QuestMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
