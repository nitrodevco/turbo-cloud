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

namespace Turbo.Ai;

/// <summary>Whose a model answer is, for the usage log.</summary>
public sealed record AiUsageContext(string Plugin, string Agent, int? PlayerId, long? ConversationId);

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
        IEnumerable<string> order = names.Length > 0
            ? names
            : current.Failover.Count > 0
                ? current.Failover
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
    public async Task<AiAnswer> SendAsync(AiStep step, AiUsageContext usage, CancellationToken ct)
    {
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
            var model = i == 0 && !string.IsNullOrWhiteSpace(step.Model) ? step.Model : provider.Model;
            var timeout =
                step.Timeout
                ?? TimeSpan.FromSeconds(
                    provider.TimeoutSeconds > 0 ? provider.TimeoutSeconds : current.TimeoutSeconds
                );
            var key = provider.NoKey ? null : Key(current, name);
            var endpoint = new AiEndpoint(name, provider.Url, key, provider.Headers, provider.Options);
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
                var answer = await driver
                    .SendAsync(
                        endpoint,
                        new AiProviderRequest(
                            model,
                            step.Instructions,
                            step.Messages,
                            step.Tools,
                            step.RequireTool,
                            step.MaxTokens,
                            step.Temperature
                        ),
                        timer.Token
                    )
                    .ConfigureAwait(false);
                var cost =
                    answer.InputTokens * provider.InputPricePerMillion / 1_000_000m
                    + answer.OutputTokens * provider.OutputPricePerMillion / 1_000_000m;
                var answered = string.IsNullOrEmpty(answer.Model) ? model : answer.Model;

                await RecordAsync(usage, name, answered, answer.InputTokens, answer.OutputTokens, cost, started, "ok")
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
                await RecordAsync(usage, name, model, 0, 0, 0, started, "timeout").ConfigureAwait(false);
            }
            catch (AiProviderException ex)
            {
                failures.Add($"{name}: {DriverHttp.Redact(ex.Message, key)}");
                await RecordAsync(usage, name, model, 0, 0, 0, started, ex.Kind).ConfigureAwait(false);
            }

            logger.LogWarning(
                "The AI provider {Provider} failed for the plugin {Plugin}: {Failure}",
                name,
                usage.Plugin,
                failures[^1]
            );
        }

        throw new AiProviderException("failed", string.Join("; ", failures));
    }

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
            : config.Keys.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
