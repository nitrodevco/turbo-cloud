using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record HabboBroadcastMessageComposer : IComposer
{
    /// <summary>The pop-up's text. The client reads a backslash and an r as a line break.</summary>
    [Id(0)]
    public required string Message { get; init; }
}
