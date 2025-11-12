using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record WiredFurniAddonEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
