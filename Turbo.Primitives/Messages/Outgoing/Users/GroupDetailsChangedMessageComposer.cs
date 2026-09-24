using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// A nudge, not an update: the client re-requests the group's details if it happens to have
/// that group open, and ignores this otherwise. Sending the details themselves instead would
/// push a window onto everybody who had never opened one.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GroupDetailsChangedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildId GuildId { get; init; }
}
