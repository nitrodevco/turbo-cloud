using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public record HanditemConfigurationMessageComposer : IComposer
{
    public required bool IsHanditemControlBlocked { get; init; }
}
