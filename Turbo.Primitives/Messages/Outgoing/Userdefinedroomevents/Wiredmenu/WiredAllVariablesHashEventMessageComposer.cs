using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredAllVariablesHashEventMessageComposer : IComposer
{
    [Id(0)]
    public required WiredVariableHash AllVariablesHash { get; init; }
}
