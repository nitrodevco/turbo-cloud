using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record HabboAchievementNotificationMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
