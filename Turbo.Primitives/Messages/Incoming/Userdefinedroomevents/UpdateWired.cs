using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public abstract record UpdateWired
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required List<int> IntParams { get; init; }

    [Id(2)]
    public required List<int> VariableIds { get; init; }

    [Id(3)]
    public required string StringParam { get; init; }

    [Id(4)]
    public required List<int> StuffIds { get; init; }

    [Id(5)]
    public required List<int> FurniSources { get; init; }

    [Id(6)]
    public required List<int> PlayerSources { get; init; }
}
