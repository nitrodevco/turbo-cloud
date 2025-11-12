using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record RestoreClientMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
