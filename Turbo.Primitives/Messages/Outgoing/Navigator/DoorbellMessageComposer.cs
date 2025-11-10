using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record DoorbellMessageComposer : IComposer
{
    public string? Username { get; init; }
}
