using Orleans;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>The answer to a call for help: its result and the text shown, empty for the client's own.</summary>
[GenerateSerializer, Immutable]
public sealed record CfhResultSnapshot
{
    [Id(0)]
    public required CfhResultType Result { get; init; }

    [Id(1)]
    public required string Message { get; init; }
}
