using System;

namespace Turbo.Pipeline.Registry;

public class PipelineContext
{
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
}
