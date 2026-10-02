using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

[GenerateSerializer, Immutable]
public sealed record ModeratorMessageComposer : IComposer
{
    [Id(0)]
    public required string Message { get; init; }

    /// <summary>A link under the message; empty for none.</summary>
    [Id(1)]
    public required string Url { get; init; }
}
