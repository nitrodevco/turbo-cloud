using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

public record LagWarningReportMessage : IMessageEvent
{
    public int WarningCount { get; init; }
}
