using System;

namespace Turbo.Pipeline.Registry;

public class PipelineContext
{
    public required IServiceProvider ServiceProvider { get; set; }
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
}
