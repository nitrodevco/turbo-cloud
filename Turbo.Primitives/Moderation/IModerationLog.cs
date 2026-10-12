using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// The hotel's record of moderation (<c>moderation_actions</c>): every action staff, a room or
/// the hotel itself takes against a player or a room, kept so staff can see what has happened
/// to a player, who did it and why. Recording never fails the action it records: a write that
/// cannot be made is logged, and the action stands.
/// </summary>
public interface IModerationLog
{
    Task RecordAsync(ModerationRecord record, CancellationToken ct);
}
