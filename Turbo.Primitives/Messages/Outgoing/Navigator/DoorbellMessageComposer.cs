using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record DoorbellMessageComposer : IComposer
{
    public string? Username { get; init; }
}
