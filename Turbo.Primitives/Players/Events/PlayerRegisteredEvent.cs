using System.Text;
using Turbo.Primitives.Events;
using Turbo.Primitives.Players.Accounts;

namespace Turbo.Primitives.Players.Events;

/// <summary>A new player account was made and saved (the site's registration, the panel's).</summary>
public sealed record PlayerRegisteredEvent : DomainEvent
{
    public required PlayerId PlayerId { get; init; }

    public required string Name { get; init; }

    /// <summary>Where the account was made from.</summary>
    public RegistrationSource Source { get; init; }

    /// <summary>
    /// The address the account was asked for from (the site's sign-up); empty when not known or
    /// staff made it. Personal data: the event's handlers read it, the event's text leaves it out.
    /// </summary>
    public string RemoteAddress { get; init; } = string.Empty;

    protected override bool PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", PlayerId = ").Append(PlayerId);
        builder.Append(", Name = ").Append(Name);
        builder.Append(", Source = ").Append(Source);

        return true;
    }
}
