using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public record WiredGetAllVariablesDiffsMessage : IMessageEvent
{
    [Id(0)]
    public required List<(string Id, int Hash)> VariableHashes { get; init; } = [];
}
