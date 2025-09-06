namespace Turbo.Pipeline.Core.Envelope;

public record EnvelopeBase<TEnvelopeData>
{
    public required TEnvelopeData Data { get; init; }
    public string? Tag { get; init; }
}
