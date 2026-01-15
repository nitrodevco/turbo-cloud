using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredAllVariablesHashEventMessageComposer : IComposer
{
    [Id(0)]
    public required int AllVariablesHash { get; init; }
}
