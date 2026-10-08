using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>The song an official song's catalog code stands for, for the catalog's song disk page.</summary>
[GenerateSerializer, Immutable]
public sealed record OfficialSongIdMessageComposer : IComposer
{
    [Id(0)]
    public required string Code { get; init; }

    [Id(1)]
    public required int SongId { get; init; }
}
