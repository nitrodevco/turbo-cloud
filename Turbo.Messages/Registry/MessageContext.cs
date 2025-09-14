using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Messages.Registry;

public class MessageContext : PipelineContext
{
    public required ISessionContext Session { get; init; }
}
