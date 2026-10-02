using Orleans;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>chat.commands</c>): the chat commands the player may use,
/// with every parameter's type, the whole set each time. The presence delivers it only to a
/// session that accepted the extension.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboCommandTreeMessage : ICapabilityComposer
{
    [Id(0)]
    public required CommandTreeSnapshot Tree { get; init; }

    public string Capability => ClientCapabilities.CHAT_COMMANDS;
}
