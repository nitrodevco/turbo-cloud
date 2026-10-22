using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Onboarding.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Nux;

/// <summary>
/// The new user gift offer: the client puts up "Choose gift option" with one step after another
/// and answers <c>NewUserExperienceGetGifts</c> once every step has a pick.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NewUserExperienceGiftOfferEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<NuxGiftStepSnapshot> Steps { get; init; }
}
