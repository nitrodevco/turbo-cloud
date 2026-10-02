namespace Turbo.Achievements;

/// <summary>
/// The definition revision an admitted fact is bound to. Revisions are immutable and kept in
/// <c>achievement_definitions</c>, so the fact stores only this key rather than the definition.
/// </summary>
internal sealed record AchievementBinding(int Id, int Revision);
