using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Talent;

[GenerateSerializer, Immutable]
public sealed record TalentTrackLevelMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
