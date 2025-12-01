using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Competition;

[GenerateSerializer, Immutable]
public sealed record CompetitionEntrySubmitResultMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
