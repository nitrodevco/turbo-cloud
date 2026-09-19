using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

/// <summary>The training tool's button grid: every command, and the ones the pet has learned.</summary>
[GenerateSerializer, Immutable]
public sealed record PetCommandsMessageComposer : IComposer
{
    [Id(0)]
    public required int PetId { get; init; }

    [Id(1)]
    public required ImmutableArray<PetCommandType> AllCommands { get; init; }

    [Id(2)]
    public required ImmutableArray<PetCommandType> EnabledCommands { get; init; }
}
