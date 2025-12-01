using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

[GenerateSerializer, Immutable]
public sealed record CommunityGoalHallOfFameMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
