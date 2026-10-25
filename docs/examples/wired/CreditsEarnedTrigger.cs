using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace WiredExamples;

/// <summary>A player earned credits: what the plugin fires, wherever the credits came from.</summary>
public sealed record CreditsEarned(int Amount) : IWiredEvent;

/// <summary>
/// "When a user earns credits": fires in the room of the player who earned them, with them as
/// the triggering user, when the plugin calls
/// <c>hotel.Wired.FireForPlayerAsync(player, new CreditsEarned(amount), ct)</c>. It opens the
/// official Game Starts editor, which has no inputs.
/// </summary>
[WiredBox("wf_trg_credits_earned")]
public sealed class CreditsEarnedTrigger : WiredTrigger<CreditsEarned>
{
    public override WiredTriggerType Editor => WiredTriggerType.GAME_STARTS;

    public override bool Matches(CreditsEarned evt, IWiredBoxConfig box) => evt.Amount > 0;
}
