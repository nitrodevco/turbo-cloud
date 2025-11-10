using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public sealed record YouArePlayingGameMessageComposer : IComposer
{
    public required bool IsPlaying { get; init; }
}
