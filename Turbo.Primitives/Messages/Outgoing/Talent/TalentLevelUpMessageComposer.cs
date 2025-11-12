using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Talent;

[GenerateSerializer, Immutable]
public sealed record TalentLevelUpMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
