namespace Turbo.Primitives.Plugins;

/// <summary>How a connection test went: whether it answered, in words, the model that answered, its tokens and how long it took.</summary>
public sealed record AiConnectionTest(
    bool Ok,
    string Message,
    string? Model,
    int InputTokens,
    int OutputTokens,
    int DurationMs
);
