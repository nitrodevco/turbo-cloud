using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record MOTDNotificationEventMessageComposer : IComposer
{
    [Id(0)]
    public required List<string> Messages { get; init; }
}
