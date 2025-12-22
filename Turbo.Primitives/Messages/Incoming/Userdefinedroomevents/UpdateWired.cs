using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

public abstract record UpdateWired
{
    public required int Id { get; init; }
    public required List<int> IntParams { get; init; }
    public required List<int> VariableIds { get; init; }
    public required string StringParam { get; init; }
    public required List<int> StuffIds { get; init; }
    public required List<int> FurniSources { get; init; }
    public required List<int> PlayerSources { get; init; }
}
