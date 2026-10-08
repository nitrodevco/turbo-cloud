using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// The chat bubble style of the message that fired "User Says Keyword" (variables-info #13: "if the user is chatting with a Zombie Hand chat bubble"); 0 for a stack something else started.
/// </summary>
public sealed class ContextChatStyleVariable(RoomGrain roomGrain) : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.chat.style";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // The official client's Creator Tools list the context variables in this order; the gaps
    // leave room for the @event.* ones still to come.
    protected override ushort Order => 40;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is PlayerChatEvent chat ? chat.StyleId : 0;
}
