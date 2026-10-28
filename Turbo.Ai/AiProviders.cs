using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Ai.Configuration;
using Turbo.Ai.Drivers;
using Turbo.Ai.Storage;
using Turbo.Contracts.Ai;
using Turbo.Primitives.Plugins;

namespace Turbo.Ai;

/// <summary>Whose a model answer is, for the usage log.</summary>
public sealed record AiUsageContext(
    string Plugin,
    string Agent,
    int? PlayerId,
    long? ConversationId
);

/// <summary>
/// The hotel's providers (<see cref="AiConfig.Providers"/>): asks them in order until one answers,
/// and logs each attempt's tokens, cost and outcome. A provider without a key (unless it takes none)
/// or with a driver the server does not have is never asked.
/// </summary>
public sealed class AiProviders(
    IOptionsMonitor<AiConfig> config,
    IEnumerable<IAiDriver> drivers,
    IAiStore store,
    TimeProvider clock,
    ILogger<AiProviders> logger
)
{
    private readonly Dictionary<string, IAiDriver> _drivers = drivers.ToDictionary(
        x => x.Name,
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>Whether any provider can be asked.</summary>
    public bool AnyUsable => Usable([]).Count > 0;

    /// <summary>The providers to try for <paramref name="names"/> (the hotel's order when empty), the unusable left out.</summary>
    public IReadOnlyList<string> Usable(ImmutableArray<string> names)
    {
        var current = config.CurrentValue;
        IEnumerable<string> order =
            names.Length > 0 ? names
            : current.Failover.Count > 0 ? current.Failover
            : current.Providers.Keys.Order(StringComparer.Ordinal);

        return
        [
            .. order
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name =>
                    current.Providers.TryGetValue(name, out var provider)
                    && _drivers.ContainsKey(provider.Driver)
                    && (provider.NoKey || !string.IsNullOrWhiteSpace(Key(current, name)))
                ),
        ];
    }

    /// <summary>
    /// Asks the step's providers in order. Throws <see cref="AiProviderException"/> naming each
    /// failure when none answers, and <see cref="OperationCanceledException"/> when
    /// <paramref name="ct"/> is cancelled.
    /// </summary>
    public Task<AiAnswer> SendAsync(AiStep step, AiUsageContext usage, CancellationToken ct) =>
        AskAsync(step, usage, null, ct);

    /// <summary>
    /// <see cref="SendAsync"/>, the answer streamed: <paramref name="text"/> is given each piece
    /// of text as it comes. The next provider is asked only while nothing has been given: one
    /// that fails part way through its answer ends the step.
    /// </summary>
    public Task<AiAnswer> StreamAsync(
        AiStep step,
        AiUsageContext usage,
        Action<string> text,
        CancellationToken ct
    ) => AskAsync(step, usage, text, ct);

    private async Task<AiAnswer> AskAsync(
        AiStep step,
        AiUsageContext usage,
        Action<string>? text,
        CancellationToken ct
    )
    {
        var streamed = false;
        Action<string>? piece = text is null
            ? null
            : t =>
            {
                streamed = true;
                text(t);
            };
        var current = config.CurrentValue;
        var names = Usable(step.Providers);

        if (names.Count == 0)
            throw new AiProviderException(
                "no_provider",
                step.Providers.Length > 0
                    ? $"none of the providers {string.Join(", ", step.Providers)} is configured with a key"
                    : "the hotel has no provider configured with a key"
            );

        var failures = new List<string>();

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            var provider = current.Providers[name];
            var driver = _drivers[provider.Driver];
            var model =
                i == 0 && !string.IsNullOrWhiteSpace(step.Model) ? step.Model : provider.Model;
            var timeout =
                step.Timeout
                ?? TimeSpan.FromSeconds(
                    provider.TimeoutSeconds > 0 ? provider.TimeoutSeconds : current.TimeoutSeconds
                );
            var key = provider.NoKey ? null : Key(current, name);
            var endpoint = new AiEndpoint(
                name,
                provider.Url,
                key,
                provider.Headers,
                provider.Options
            );
            var started = Stopwatch.GetTimestamp();

            if (string.IsNullOrWhiteSpace(model))
            {
                failures.Add($"{name}: no model is configured");

                continue;
            }

            using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);

            timer.CancelAfter(timeout);

            try
            {
                var request = new AiProviderRequest(
                    model,
                    step.Instructions,
                    step.Messages,
                    step.Tools,
                    step.RequireTool,
                    step.MaxTokens,
                    step.Temperature
                );
                var answer = await (
                    piece is null
                        ? driver.SendAsync(endpoint, request, timer.Token)
                        : driver.StreamAsync(endpoint, request, piece, timer.Token)
                ).ConfigureAwait(false);
                var cost =
                    answer.InputTokens * provider.InputPricePerMillion / 1_000_000m
                    + answer.OutputTokens * provider.OutputPricePerMillion / 1_000_000m;
                var answered = string.IsNullOrEmpty(answer.Model) ? model : answer.Model;

                await RecordAsync(
                        usage,
                        name,
                        answered,
                        answer.InputTokens,
                        answer.OutputTokens,
                        cost,
                        started,
                        "ok"
                    )
                    .ConfigureAwait(false);

                return new AiAnswer(
                    answer.Message,
                    answer.Finish,
                    new AiUsage(answer.InputTokens, answer.OutputTokens, cost),
                    name,
                    answered
                );
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                failures.Add($"{name}: took longer than {timeout.TotalSeconds:0} s");
                await RecordAsync(usage, name, model, 0, 0, 0, started, "timeout")
                    .ConfigureAwait(false);
            }
            catch (AiProviderException ex)
            {
                failures.Add($"{name}: {DriverHttp.Redact(ex.Message, key)}");
                await RecordAsync(usage, name, model, 0, 0, 0, started, ex.Kind)
                    .ConfigureAwait(false);
            }

            logger.LogWarning(
                "The AI provider {Provider} failed for the plugin {Plugin}: {Failure}",
                name,
                usage.Plugin,
                failures[^1]
            );

            // Its text has been shown: another provider's answer would follow it, not replace it.
            if (streamed)
                break;
        }

        throw new AiProviderException("failed", string.Join("; ", failures));
    }

    /// <summary>
    /// Asks the provider <paramref name="name"/> for one token with its key and model (the
    /// panel's connection test); the attempt is kept in the usage log as the panel's.
    /// </summary>
    public async Task<AiConnectionTest> TestAsync(string name, CancellationToken ct)
    {
        var current = config.CurrentValue;
        var found = current.Providers.FirstOrDefault(x =>
            string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)
        );

        if (found.Value is not { } provider)
            return Untried($"There is no provider named {name}.");

        name = found.Key;

        if (!_drivers.TryGetValue(provider.Driver, out var driver))
            return Untried(
                $"The server has no driver \"{provider.Driver}\": use anthropic or openai-compatible."
            );

        var key = provider.NoKey ? null : Key(current, name);

        if (!provider.NoKey && string.IsNullOrWhiteSpace(key))
            return Untried("It has no key: enter one, or mark it as taking none.");

        if (string.IsNullOrWhiteSpace(provider.Model))
            return Untried("It has no model.");

        var usage = new AiUsageContext(IAiOverview.TEST_PLUGIN, "connection-test", null, null);
        var timeout = TimeSpan.FromSeconds(
            provider.TimeoutSeconds > 0 ? provider.TimeoutSeconds : current.TimeoutSeconds
        );
        var started = Stopwatch.GetTimestamp();
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);

        timer.CancelAfter(timeout);

        try
        {
            var answer = await driver
                .SendAsync(
                    new AiEndpoint(name, provider.Url, key, provider.Headers, provider.Options),
                    new AiProviderRequest(
                        provider.Model,
                        string.Empty,
                        [AiMessage.User("Say OK.")],
                        [],
                        false,
                        1,
                        null
                    ),
                    timer.Token
                )
                .ConfigureAwait(false);
            var model = string.IsNullOrEmpty(answer.Model) ? provider.Model : answer.Model;
            var cost =
                answer.InputTokens * provider.InputPricePerMillion / 1_000_000m
                + answer.OutputTokens * provider.OutputPricePerMillion / 1_000_000m;
            var ms = (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            await RecordAsync(
                    usage,
                    name,
                    model,
                    answer.InputTokens,
                    answer.OutputTokens,
                    cost,
                    started,
                    "ok"
                )
                .ConfigureAwait(false);

            return new AiConnectionTest(
                true,
                $"{model} answered in {ms} ms.",
                model,
                answer.InputTokens,
                answer.OutputTokens,
                ms
            );
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await RecordAsync(usage, name, provider.Model, 0, 0, 0, started, "timeout")
                .ConfigureAwait(false);

            return Failed($"It took longer than {timeout.TotalSeconds:0} s.", started);
        }
        catch (AiProviderException ex)
        {
            await RecordAsync(usage, name, provider.Model, 0, 0, 0, started, ex.Kind)
                .ConfigureAwait(false);

            return Failed($"It failed: {DriverHttp.Redact(ex.Message, key)}.", started);
        }

        static AiConnectionTest Untried(string why) => new(false, why, null, 0, 0, 0);

        static AiConnectionTest Failed(string why, long started) =>
            new(false, why, null, 0, 0, (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>The usage log's outcome for a run that failed going on after its approval.</summary>
    public const string RESUME_FAILED = "resume_failed";

    /// <summary>Logs a failure no provider answered for (no provider or model; no tokens).</summary>
    public Task RecordFailureAsync(AiUsageContext usage, string outcome) =>
        RecordAsync(usage, string.Empty, string.Empty, 0, 0, 0, Stopwatch.GetTimestamp(), outcome);

    private async Task RecordAsync(
        AiUsageContext usage,
        string provider,
        string model,
        int input,
        int output,
        decimal cost,
        long started,
        string outcome
    )
    {
        try
        {
            await store
                .RecordUsageAsync(
                    new AiUsageRecord(
                        usage.Plugin,
                        usage.Agent,
                        provider,
                        model,
                        usage.PlayerId,
                        usage.ConversationId,
                        input,
                        output,
                        cost,
                        (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        outcome,
                        clock.GetUtcNow().UtcDateTime
                    ),
                    CancellationToken.None
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not log AI usage for the plugin {Plugin}", usage.Plugin);
        }
    }

    private static string? Key(AiConfig config, string name) =>
        config.Keys.TryGetValue(name, out var key)
            ? key
            : config
                .Keys.FirstOrDefault(x =>
                    string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)
                )
                .Value;
}
