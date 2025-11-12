using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Competition;

[GenerateSerializer, Immutable]
public sealed record IsUserPartOfCompetitionMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
