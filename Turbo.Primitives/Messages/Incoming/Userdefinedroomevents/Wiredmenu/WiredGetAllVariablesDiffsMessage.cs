using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public record WiredGetAllVariablesDiffsMessage : IMessageEvent
{
    [Id(0)]
    public required List<(
        WiredVariableId Id,
        WiredVariableHash Hash
    )> VariableIdsWithHash { get; init; } = [];
}
