using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Help;

[GenerateSerializer, Immutable]
public sealed record GuideTicketCreationResultMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
