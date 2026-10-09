namespace Turbo.Primitives.Quests.Enums;

/// <summary>Something a player did that a daily task can count.</summary>
public enum DailyTaskActivity
{
    /// <summary>Entered a room someone else owns; the value is the room id.</summary>
    RoomVisit = 0,

    /// <summary>Double-clicked a furni; the value is its definition name.</summary>
    FurniUse = 1,
}
