using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// The labels Habbo gives Habbicon ids on <c>@habbicon</c> and <c>@event.habbicon</c>, in the order
/// and spelling it sends them (official client capture, WIN63-202610121709). They are Habbo's own
/// names, not the hotel texts ("Toast Is Fine" where the texts say "Toast is Fine").
/// </summary>
internal static class WiredHabbiconTextConnectors
{
    public static Dictionary<WiredVariableValue, string> Create() =>
        new()
        {
            [64] = "Winky Toast",
            [65] = "Sad Toast",
            [66] = "Teary Toast",
            [67] = "Grumpy Toast",
            [68] = "Sleepy Toast",
            [69] = "Shocked Toast",
            [70] = "Flustered Toast",
            [71] = "Toast Is Fine",
            [28] = "Duck",
            [29] = "Happy Duck",
            [30] = "Sad Duck",
            [31] = "Shocked Duck",
            [32] = "Thinking Duck",
            [33] = "Hear No Duck",
            [34] = "See No Duck",
            [35] = "Say No Duck",
            [36] = "Angel Duck",
            [37] = "Devil Duck",
            [38] = "Spinning Duck",
            [39] = "Cool Duck",
            [40] = "Pleased Duck",
            [41] = "Laughing Duck",
            [42] = "Grimacing Duck",
            [43] = "Devious Duck",
            [44] = "Metal Duck",
            [45] = "Pleading Duck",
            [46] = "Silly Duck",
            [47] = "Winking Duck",
            [48] = "Party Duck",
            [49] = "Duck In Love",
            [50] = "Frank",
            [51] = "Smiling Frank",
            [52] = "Happy Frank",
            [53] = "Sad Frank",
            [54] = "Scared Frank",
            [55] = "Surprised Frank",
            [56] = "Thinking Frank",
            [57] = "Silly Frank",
            [58] = "Relived Frank",
            [59] = "Winking Frank",
            [60] = "Starry-eyed Frank",
            [61] = "Toast",
            [62] = "Happy Toast",
            [63] = "Cutesy Toast",
        };
}
