using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Players.Onboarding.Snapshots;

/// <summary>
/// One gift of a step (<c>NewUserExperienceGift</c>): its picture, a path under the client's
/// <c>image.library.url</c> (empty for none), and the products it is made of.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NuxGiftOptionSnapshot
{
    [Id(0)]
    public required string ThumbnailUrl { get; init; }

    [Id(1)]
    public required ImmutableArray<NuxGiftProductSnapshot> Products { get; init; }
}
