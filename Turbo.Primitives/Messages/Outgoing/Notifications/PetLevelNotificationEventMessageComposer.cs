using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record PetLevelNotificationEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
