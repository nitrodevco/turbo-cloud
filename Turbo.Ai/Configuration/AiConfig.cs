using System.Collections.Generic;
using Turbo.Primitives.Settings;

namespace Turbo.Ai.Configuration;

/// <summary>
/// The hotel's language models for plugins (<c>docs/ai-sdk.md</c>): the providers it brings, their
/// keys, the order they are tried in, which plugins may use them and what a player may spend.
/// Nothing is sent anywhere until a provider has a key (or, for a server on its own machine, an
/// address that needs none). Every value applies at once.
/// </summary>
public class AiConfig
{
    public const string SECTION_NAME = "Turbo:Ai";

    /// <summary>
    /// The providers, by a name of the hotel's choosing: <c>{ "claude": { "Driver": "anthropic",
    /// "Model": "claude-haiku-5-5" }, "local": { "Driver": "openai-compatible", "Url":
    /// "http://localhost:11434/v1", "Model": "llama3.1", "NoKey": true } }</c>.
    /// </summary>
    [LiveSetting]
    public Dictionary<string, AiProviderConfig> Providers { get; init; } = [];

    /// <summary>The providers' API keys, by provider name. Never shown, never logged; replaced whole.</summary>
    [SecretSetting]
    [LiveSetting]
    public Dictionary<string, string> Keys { get; init; } = [];

    /// <summary>
    /// The providers to try, in order: when one fails (down, slow, over its limit, refusing the
    /// key), the next is asked the same step. Empty tries every provider by name.
    /// </summary>
    [LiveSetting]
    public List<string> Failover { get; init; } = [];

    /// <summary>
    /// The plugins (by key) allowed to use models. A plugin must also declare <c>"ai"</c> in its
    /// manifest, with what it uses them for; the Plugins page shows it.
    /// </summary>
    [LiveSetting]
    public List<string> Plugins { get; init; } = [];

    /// <summary>Tokens (in and out) one player's calls may use a day, every plugin together; 0 for no limit.</summary>
    [LiveSetting]
    public int PlayerDailyTokens { get; init; }

    /// <summary>Runs one player's calls may start a minute, every plugin together; 0 for no limit.</summary>
    [LiveSetting]
    public int PlayerRunsPerMinute { get; init; } = 6;

    /// <summary>Runs one plugin may start a minute; 0 for no limit.</summary>
    [LiveSetting]
    public int PluginRunsPerMinute { get; init; } = 120;

    /// <summary>The most steps any run takes, whatever its agent asks.</summary>
    [LiveSetting]
    public int MaxSteps { get; init; } = 20;

    /// <summary>
    /// The most messages a conversation keeps: older ones are dropped before a run, whole turns at
    /// a time (an agent may summarise them instead, <c>AiSummarize</c>).
    /// </summary>
    [LiveSetting]
    public int MemoryMessages { get; init; } = 40;

    /// <summary>How long one model answer may take, in seconds, for a provider that does not say.</summary>
    [LiveSetting]
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>Resumed approval runs proceeding at once per server; applies after a restart.</summary>
    public int ResumesAtOnce { get; init; } = 4;

    /// <summary>Maximum queued and active approval continuations per server; overflow stays available for manual resume. Applies after a restart.</summary>
    public int ResumeQueueCapacity { get; init; } = 128;
}

/// <summary>One provider: its driver, address, model, timeout, prices and options.</summary>
public class AiProviderConfig
{
    /// <summary><c>anthropic</c>, or <c>openai-compatible</c> (OpenAI, and any server speaking its chat completions: a local one included).</summary>
    public string Driver { get; init; } = string.Empty;

    /// <summary>
    /// Its address: for <c>anthropic</c> <c>https://api.anthropic.com</c> by default; for
    /// <c>openai-compatible</c> the base its <c>/chat/completions</c> is under,
    /// <c>https://api.openai.com/v1</c> by default.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>The model it is asked for unless an agent or a call names another.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>It takes no key (a server on the hotel's own machine): it is used without one.</summary>
    public bool NoKey { get; init; }

    /// <summary>How long one answer may take, in seconds; 0 for <c>Turbo:Ai:TimeoutSeconds</c>.</summary>
    public int TimeoutSeconds { get; init; }

    /// <summary>What a million input tokens cost, in the hotel's money, for the usage log and budgets.</summary>
    public decimal InputPricePerMillion { get; init; }

    /// <summary>What a million output tokens cost.</summary>
    public decimal OutputPricePerMillion { get; init; }

    /// <summary>
    /// More fields for the request's body, each value JSON (<c>{ "top_p": "0.9" }</c>); text that
    /// is not JSON is sent as text. A field the SDK writes itself is not replaced.
    /// </summary>
    public Dictionary<string, string> Options { get; init; } = [];

    /// <summary>More request headers (a beta flag, an organisation). Not for the key.</summary>
    public Dictionary<string, string> Headers { get; init; } = [];
}
