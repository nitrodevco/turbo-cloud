using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// How the message that fired "User Says Keyword" was said: 0 talk, 1 shout, 2 whisper (variables-info #9); 0 for a stack something else started.
/// </summary>
public sealed class ContextChatTypeVariable(RoomGrain roomGrain) : ContextVariable(roomGrain)
{
    protected override string VariableName => "@chat_type";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // sirjonasxx's overview (variables-info #9) lists the context variables in this order.
    protected override ushort Order => 5;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is PlayerChatEvent chat ? (int)chat.ChatType : 0;
}
