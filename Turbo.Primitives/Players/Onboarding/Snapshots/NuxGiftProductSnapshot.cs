using Orleans;

namespace Turbo.Primitives.Players.Onboarding.Snapshots;

/// <summary>
/// A product of a gift (<c>NewUserExperienceGiftProduct</c>): the client names it by the text
/// <see cref="LocalizationKey"/> when there is one, else by the product data of
/// <see cref="ProductCode"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NuxGiftProductSnapshot
{
    [Id(0)]
    public required string ProductCode { get; init; }

    [Id(1)]
    public required string LocalizationKey { get; init; }
}
