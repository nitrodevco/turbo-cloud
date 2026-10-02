using System.Collections.Immutable;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// A set of achievement definitions that ships together, such as the Habbo catalog or a plugin's
/// seasonal set. A pack only adds: it installs the definitions whose key and id are not in the
/// hotel yet and never touches one the hotel already has, so a hotel's edits and retirements
/// survive a new version of the pack, and a new version's new achievements still arrive.
/// </summary>
public interface IAchievementPack
{
    /// <summary>A short permanent name, lowercase, such as <c>habbo</c>.</summary>
    string Key { get; }

    /// <summary>Raised whenever the definitions change, so the install is recorded under a new audit id.</summary>
    int Version { get; }

    /// <summary>The ids the pack's definitions use. Packs may not overlap each other.</summary>
    ImmutableArray<AchievementIdRange> IdRanges { get; }

    ImmutableArray<AchievementDefinition> Definitions { get; }
}
