using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Messaging.Abstractions.Registry;

public class MessageContext : PipelineContext
{
    public required ISessionContext Session { get; init; }
}
