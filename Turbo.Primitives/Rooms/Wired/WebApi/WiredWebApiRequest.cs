using Orleans;

namespace Turbo.Primitives.Rooms.Wired.WebApi;

/// <summary>
/// One Web API call as the room is asked to answer it: the route's parts, the key headers
/// (<c>X-Wired-Read-Key</c>, <c>X-Wired-Write-Key</c>), the query and the JSON body as sent.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredWebApiRequest
{
    [Id(0)]
    public required WiredWebApiOperation Operation { get; init; }

    [Id(1)]
    public string? ReadKey { get; init; }

    [Id(2)]
    public string? WriteKey { get; init; }

    /// <summary><c>user</c> or <c>furni</c>.</summary>
    [Id(3)]
    public string Scope { get; init; } = string.Empty;

    [Id(4)]
    public string VariableName { get; init; } = string.Empty;

    /// <summary><c>users</c>, <c>pets</c>, <c>bots</c>, <c>furni</c>, <c>furni-bc</c>, <c>wall-items</c> or <c>wall-items-bc</c>.</summary>
    [Id(5)]
    public string TargetKind { get; init; } = string.Empty;

    [Id(6)]
    public string EntityId { get; init; } = string.Empty;

    [Id(7)]
    public string? Body { get; init; }

    [Id(8)]
    public int? Page { get; init; }

    [Id(9)]
    public int? Size { get; init; }

    [Id(10)]
    public string? OrderBy { get; init; }

    [Id(11)]
    public string? OrderDir { get; init; }

    [Id(12)]
    public string? Name { get; init; }

    [Id(13)]
    public string? UniqueId { get; init; }
}
