using System.Collections.Generic;
using System.Linq;
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
        WiredVariableFlags.HasValue | WiredVariableFlags.HasTextConnector;

    // The bubble names hhus sends with the variable (WiredAllVariablesDiffs, WIN63-202610121709).
    private static readonly Dictionary<int, string> StyleNames = new()
    {
        [0] = "Default",
        [1] = "Generic",
        [2] = "Robot",
        [130] = "Team Red",
        [3] = "Normal Red",
        [131] = "Team Blue",
        [4] = "Normal Blue",
        [132] = "Team Yellow",
        [5] = "Normal Yellow",
        [133] = "Team Green",
        [6] = "Normal Green",
        [7] = "Normal Grey",
        [8] = "Fortune Teller",
        [9] = "Zombie Hand",
        [10] = "Skeleton",
        [11] = "Normal Sky Blue",
        [12] = "Normal Pink",
        [13] = "Normal Purple",
        [14] = "Normal Dark Yellow",
        [15] = "Normal Dark Turquoise",
        [16] = "Hearts",
        [17] = "Gothicrose",
        [18] = "Stickingplaster",
        [19] = "Piglet",
        [20] = "Sausagedog",
        [21] = "Firingmylazer",
        [22] = "Dragon",
        [23] = "Staff",
        [24] = "Bats",
        [25] = "Console",
        [26] = "Steampunk Pipe",
        [27] = "Storm",
        [28] = "Parrot",
        [29] = "Pirate",
        [30] = "Bot Guide",
        [31] = "Bot Rentable",
        [32] = "Skeleton Stock",
        [33] = "Bot Frank Large",
        [34] = "Notification",
        [35] = "Goat",
        [36] = "Santa",
        [37] = "Ambassador",
        [38] = "Radio",
        [200] = "Notification Red",
        [201] = "Notification Green",
        [202] = "Notification Blue",
        [210] = "Notification Alert",
        [211] = "Notification Info",
        [212] = "Notification Warning",
        [220] = "Notification Wrong",
        [221] = "Notification Wrong Circle",
        [222] = "Notification Correct",
        [223] = "Notification Correct Circle",
        [224] = "Notification Question Mark",
        [225] = "Notification Question Mark Circle",
        [226] = "Notification Arrow Up",
        [227] = "Notification Arrow Up Circle",
        [228] = "Notification Arrow Down",
        [229] = "Notification Arrow Down Circle",
        [250] = "Notification Skull",
        [251] = "Notification Skull 2",
        [252] = "Notification Magnifier",
        [253] = "Wired Link Speaker",
    };

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        StyleNames.ToDictionary(x => WiredVariableValue.Parse(x.Key), x => x.Value);

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is PlayerChatEvent chat ? chat.StyleId : 0;
}
