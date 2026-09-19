using Orleans;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record BadgeLeaderboardResultMessageComposer : IComposer
{
    [Id(0)]
    public required BadgeLeaderboardPageSnapshot Page { get; init; }
}
