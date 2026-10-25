namespace Turbo.Primitives.Bots.Enums;

/// <summary>
/// Bot skills as the client numbers them: the Users packet lists a bot's skills and the context
/// menu offers a button per skill; <c>CommandBot</c> names the skill being used.
/// </summary>
public enum BotSkillType
{
    Generic = 0,

    /// <summary>"Dress up": the bot copies its owner's current look.</summary>
    DressUp = 1,

    /// <summary>Chat setup: lines, automatic chat, delay and sentence mixing.</summary>
    SetupChat = 2,
    RandomWalk = 3,
    Dance = 4,
    ChangeName = 5,
    ServeBeverage = 6,
    ClientLink = 7,
    NuxProceed = 8,
    NuxTakeTour = 10,

    /// <summary>Hides the pick-up button; the bot belongs to the hotel.</summary>
    NoPickUp = 12,
    NavigatorSearch = 14,

    // Habbo lists 19-23 on every player bot (official client capture, all 133 recorded), but no
    // client menu reads them (Flash RentableBotMenuView), so what they stand for is not known.
    Unnamed19 = 19,
    Unnamed20 = 20,
    Unnamed21 = 21,
    Unnamed22 = 22,
    Unnamed23 = 23,
    DonateFurnitureToUser = 24,
    DonateFurnitureToAll = 25,
}
