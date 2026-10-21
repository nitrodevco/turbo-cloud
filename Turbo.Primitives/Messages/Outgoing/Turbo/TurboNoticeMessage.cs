using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>notices.live</c>): one of the client's own notices under the
/// purse (Habbo's <c>NewFeatureNotification</c>), put up or taken down while the client runs. Habbo
/// reads its notices from the external variables once, as the client starts; this carries the same
/// variables, so a client already in the hotel shows it the same way.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboNoticeMessage : ICapabilityComposer
{
    /// <summary>The notice's key, as in <c>notifications.new_feature.active</c>.</summary>
    [Id(0)]
    public required string Key { get; init; }

    /// <summary>Whether it is put up; false takes it down.</summary>
    [Id(1)]
    public required bool Shown { get; init; }

    /// <summary>
    /// Its variables by name, each standing for <c>notifications.new_feature.&lt;name&gt;.&lt;key&gt;</c>:
    /// <c>type</c>, <c>count_down_to</c>, <c>expiry</c>, <c>external_link</c> and the others the
    /// notices read.
    /// </summary>
    [Id(2)]
    public required ImmutableDictionary<string, string> Variables { get; init; }

    /// <summary>Its text, <c>notifications.new_feature.&lt;key&gt;.desc</c>.</summary>
    [Id(3)]
    public required string Description { get; init; }

    public string Capability => ClientCapabilities.LIVE_NOTICES;
}
