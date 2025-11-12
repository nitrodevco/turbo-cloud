using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredVariablesForObjectEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
