using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record IsFirstLoginOfDayMessage : IComposer
{
    public required bool IsFirstLoginOfDay { get; init; }
}
