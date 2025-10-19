using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record NavigatorDeleteSavedSearchMessage : IMessageEvent
{
    public int SearchID { get; init; }
}
