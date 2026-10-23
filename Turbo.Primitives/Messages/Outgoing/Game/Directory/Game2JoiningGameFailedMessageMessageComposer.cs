using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2JoiningGameFailedMessageMessageComposer : IComposer
{
    /// <summary>A <c>GameCenter.JOIN_FAILED_*</c> reason.</summary>
    [Id(0)]
    public required int Reason { get; init; }
}
