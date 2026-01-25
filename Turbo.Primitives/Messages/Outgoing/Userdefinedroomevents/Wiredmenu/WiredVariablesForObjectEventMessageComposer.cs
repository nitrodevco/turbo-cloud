using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredVariablesForObjectEventMessageComposer : IComposer
{
    [Id(0)]
    public required WiredVariableTargetType TargetType { get; init; }

    [Id(1)]
    public required int TargetId { get; init; }

    [Id(2)]
    public required List<(WiredVariableId id, int value)> VariableValues { get; init; }

    [Id(3)]
    public required List<int> ConfiguredInWireds { get; init; }
}
