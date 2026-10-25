using System.Collections.Immutable;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Plugins;

namespace Turbo.Primitives.Messages.Incoming.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (part of <c>client.modules</c>): what a client found running the
/// plugins' client halves - a module it refused or that threw, a template patch that matched
/// nothing or clashed, the ops a plugin's patches applied - for the extension inspector
/// (<see cref="IClientExtensionReports"/>). Never answered.
/// </summary>
public record TurboClientExtensionReportMessage : IMessageEvent
{
    /// <summary>The most lines one message is read for; the rest is dropped. A protocol bound, not a tunable.</summary>
    public const int MAX_REPORTS = 32;

    public required ImmutableArray<ClientExtensionReport> Reports { get; init; }
}
