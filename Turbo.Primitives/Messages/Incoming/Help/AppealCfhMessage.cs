using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Help;

public record AppealCfhMessage : IMessageEvent
{
    public int ReportId { get; init; }
}
