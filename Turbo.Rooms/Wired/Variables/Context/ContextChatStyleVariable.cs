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
    protected override string VariableName => "@chat_style";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // sirjonasxx's overview (variables-info #9) lists the context variables in this order.
    protected override ushort Order => 4;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is PlayerChatEvent chat ? chat.StyleId : 0;
}
