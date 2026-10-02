using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>chat.commands</c>): the answer to
/// <c>TurboCommandSuggestMessage</c>, echoing its request id so a client drops a stale one.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboCommandSuggestionsMessage : ICapabilityComposer
{
    [Id(0)]
    public required int RequestId { get; init; }

    [Id(1)]
    public required ImmutableArray<string> Values { get; init; }

    public string Capability => ClientCapabilities.CHAT_COMMANDS;
}
