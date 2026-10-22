using System.Collections.Immutable;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Onboarding.Snapshots;

namespace Turbo.Primitives.Messages.Incoming.Nux;

/// <summary>The gifts a new player picked, one per step of the offer.</summary>
public record NewUserExperienceGetGiftsMessage : IMessageEvent
{
    public required ImmutableArray<NuxGiftSelectionSnapshot> Selections { get; init; }
}
