using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>chat.commands</c>): a client asking for the values of one
/// parameter of a command that begin with <see cref="Prefix"/>.
/// </summary>
public record TurboCommandSuggestMessage : IMessageEvent
{
    public required int RequestId { get; init; }

    public required string Command { get; init; }

    /// <summary>The parameter's index in the command tree.</summary>
    public required int Parameter { get; init; }

    public required string Prefix { get; init; }

    public string Syntax { get; init; } = string.Empty;
    public string ArgumentText { get; init; } = string.Empty;
}
