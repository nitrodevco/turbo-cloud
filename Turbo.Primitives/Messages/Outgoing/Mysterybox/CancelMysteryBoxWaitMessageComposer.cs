using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Mysterybox;

[GenerateSerializer, Immutable]
public sealed record CancelMysteryBoxWaitMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
