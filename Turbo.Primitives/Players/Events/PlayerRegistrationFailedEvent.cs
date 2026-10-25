using System.Text;
using Turbo.Primitives.Events;
using Turbo.Primitives.Players.Accounts;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// An account was asked for and not made: the name broke the rules or was taken, the motto or
/// figure was too long, or a registration policy refused it. No player was made.
/// </summary>
public sealed record PlayerRegistrationFailedEvent : DomainEvent
{
    /// <summary>The name asked for, as it was given.</summary>
    public required string Name { get; init; }

    /// <summary>The refusal in words, as the one asking was told.</summary>
    public required string Reason { get; init; }

    public RegistrationSource Source { get; init; }

    /// <summary>
    /// The address it was asked for from; empty when not known. Personal data: the event's
    /// handlers read it, the event's text leaves it out.
    /// </summary>
    public string RemoteAddress { get; init; } = string.Empty;

    protected override bool PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", Name = ").Append(Name);
        builder.Append(", Reason = ").Append(Reason);
        builder.Append(", Source = ").Append(Source);

        return true;
    }
}
