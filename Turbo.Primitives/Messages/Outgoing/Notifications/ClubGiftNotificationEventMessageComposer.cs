using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

/// <summary>
/// Tells a player on login that club gifts are waiting for them. The client says nothing for a
/// count below one, so it is only worth sending when there is something to collect.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ClubGiftNotificationEventMessageComposer : IComposer
{
    [Id(0)]
    public required int NumGifts { get; init; }
}
