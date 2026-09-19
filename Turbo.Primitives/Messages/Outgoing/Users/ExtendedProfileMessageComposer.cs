using Orleans;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record ExtendedProfileMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerExtendedProfileSnapshot Profile { get; init; }

    /// <summary>The badge figures on the profile, read from the player's inventory.</summary>
    [Id(1)]
    public required PlayerBadgeSummarySnapshot Badges { get; init; }
}
