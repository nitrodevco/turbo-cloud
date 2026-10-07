using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

/// <summary>Confirms a wired chest preferences save: the general ones, or the notification ones.</summary>
[GenerateSerializer, Immutable]
public sealed record ChestPreferencesUpdateSuccessMessageComposer : IComposer
{
    [Id(0)]
    public required int ChestId { get; init; }

    [Id(1)]
    public required bool IsNotificationPreferences { get; init; }
}
