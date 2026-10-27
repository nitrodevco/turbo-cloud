using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Ai;

/// <summary>
/// Language models for a plugin (<c>docs/ai-sdk.md</c>): run an agent (<see cref="AiAgent"/>), ask
/// once, or ask for a typed answer. The hotel picks the providers and brings their keys; a plugin
/// names none and holds no key. Take it by constructor in any plugin class. A plugin uses it only
/// when its manifest declares <c>"ai"</c> and the hotel's owner allowed it (<c>Turbo:Ai:Plugins</c>);
/// nothing is sent anywhere until a provider is configured. Test with <c>Turbo.Testing.AiFake</c>.
/// </summary>
public interface IAi
{
    /// <summary>Whether a call can reach a model now, and if not, why.</summary>
    AiAvailability Availability { get; }

    /// <summary>
    /// Runs <paramref name="agent"/> on <paramref name="prompt"/>: the model answers, calls the
    /// agent's tools (as <see cref="AiCall.As"/>, never more), reads what they answer and goes on
    /// until it is done or a limit stops it (<see cref="AiResponse.Stop"/>). A call that can't run
    /// answers why in its response; it never throws for a model, a provider or a limit.
    /// </summary>
    Task<AiResponse> PromptAsync(
        AiAgent agent,
        AiPrompt prompt,
        AiCall? call = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Runs an agent that answers with <typeparamref name="TOutput"/>: the answer is checked against
    /// the type's schema, and one that does not fit is sent back to the model with each problem.
    /// </summary>
    Task<AiResponse<TOutput>> PromptAsync<TOutput>(
        AiAgent<TOutput> agent,
        AiPrompt prompt,
        AiCall? call = null,
        CancellationToken ct = default
    )
        where TOutput : class;

    /// <summary>One question, no agent class: <paramref name="instructions"/> as its instructions and no tools.</summary>
    Task<AiResponse> AskAsync(
        AiPrompt prompt,
        string? instructions = null,
        AiCall? call = null,
        CancellationToken ct = default
    );

    /// <summary>One question answered as <typeparamref name="TOutput"/> (a record).</summary>
    Task<AiResponse<TOutput>> AskAsync<TOutput>(
        AiPrompt prompt,
        string? instructions = null,
        AiCall? call = null,
        CancellationToken ct = default
    )
        where TOutput : class;

    /// <summary>
    /// Goes on with a run that waited on an approval (<see cref="AiStopReason.AwaitingApproval"/>)
    /// once it is decided: the tool runs if it was approved, the model is told if it was not. A run
    /// is resumed by itself as the approval is decided (<see cref="AiAgent.ResumedAsync"/>); call
    /// this for one whose plugin was not loaded then. <paramref name="call"/> must name the
    /// conversation's owner (<see cref="AiCall.Memory"/>).
    /// </summary>
    Task<AiResponse> ResumeAsync(AiAgent agent, AiCall call, CancellationToken ct = default);

    /// <summary>Deletes the conversation <paramref name="memory"/> names (a player asked to be forgotten).</summary>
    Task<bool> ForgetAsync(AiAgent agent, AiMemory memory, CancellationToken ct = default);
}

/// <summary>Whether a plugin's call can reach a model.</summary>
public enum AiAvailability
{
    /// <summary>A provider is configured and the plugin may use it.</summary>
    Ready,

    /// <summary>The plugin's manifest does not declare <c>"ai"</c>: it may not use models.</summary>
    NotDeclared,

    /// <summary>The hotel's owner has not allowed the plugin (<c>Turbo:Ai:Plugins</c>).</summary>
    NotAllowed,

    /// <summary>The hotel has no provider with a key: nothing is sent anywhere.</summary>
    NoProvider,
}
