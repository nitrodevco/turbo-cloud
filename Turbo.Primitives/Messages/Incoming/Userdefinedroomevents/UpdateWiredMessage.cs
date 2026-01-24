using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public record UpdateWiredMessage : IMessageEvent
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required List<int> IntParams { get; init; }

    [Id(2)]
    public required string StringParam { get; init; }

    [Id(3)]
    public required List<int> StuffIds { get; init; }

    [Id(4)]
    public required List<object> DefinitionSpecifics { get; init; }

    [Id(5)]
    public required List<WiredFurniSourceType[]> FurniSources { get; init; }

    [Id(6)]
    public required List<WiredPlayerSourceType[]> PlayerSources { get; init; }

    [Id(7)]
    public required List<long> VariableIds { get; init; }

    [Id(8)]
    public required List<object> TypeSpecifics { get; init; }
}
