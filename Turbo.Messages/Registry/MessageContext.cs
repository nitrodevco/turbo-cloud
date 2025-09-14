using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Registry;

namespace Turbo.Messages.Registry;

public class MessageContext : PipelineContext
{
    public required ISessionContext Session { get; init; }
}
