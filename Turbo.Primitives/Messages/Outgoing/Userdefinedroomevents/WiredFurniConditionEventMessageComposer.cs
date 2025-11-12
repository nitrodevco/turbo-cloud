using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record WiredFurniConditionEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
