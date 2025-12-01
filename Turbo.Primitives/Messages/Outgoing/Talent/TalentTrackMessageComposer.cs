using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Talent;

[GenerateSerializer, Immutable]
public sealed record TalentTrackMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
