using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record WiredFurniSelectorEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
