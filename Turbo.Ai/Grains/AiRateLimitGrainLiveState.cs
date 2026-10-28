namespace Turbo.Ai;

internal sealed class AiRateLimitGrainLiveState
{
    public RunWindow Window { get; } = new();
}
