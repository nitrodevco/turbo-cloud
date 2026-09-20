using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

/// <summary>
/// One of the hotel's own notifications. The client looks the type up as
/// <c>notification.&lt;type&gt;</c> in its external variables to learn how to draw it (a bubble, a
/// pop-up, which image) and as <c>notification.&lt;type&gt;.message</c> and friends in its texts,
/// then fills those texts from <see cref="Parameters"/>. So a type the client has no variable
/// for is drawn as nothing: the keys here have to exist in the hotel's client data.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NotificationDialogMessageComposer : IComposer
{
    [Id(0)]
    public required string NotificationType { get; init; }

    /// <summary>
    /// Placeholders for the localised text, and overrides for anything the external variable
    /// would otherwise decide. Usually empty.
    /// </summary>
    [Id(1)]
    public ImmutableDictionary<string, string> Parameters { get; init; } =
        ImmutableDictionary<string, string>.Empty;
}
