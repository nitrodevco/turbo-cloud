using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Policies;

/// <summary>
/// A policy over a new account, asked once the hotel's own checks have passed (the name's rules,
/// the word filter, a name nobody has, the motto's and figure's lengths) and before the player is
/// made: it allows, or refuses with a reason the one asking is shown. A cap on accounts from one
/// address, a look the hotel wants, a name list of its own. A public class implementing it is
/// found in a plugin's assembly as it loads and taken away as it unloads; see
/// <see cref="IGamePolicies"/> for the order several run in. The first refusal is the answer; a
/// policy that throws is logged and passed over (it allows). <see cref="RegistrationRequest.Source"/>
/// says who is asking, so a policy may let staff through.
/// </summary>
public interface IRegistrationPolicy
{
    /// <summary>Lower runs first. 0 by default.</summary>
    int Order => 0;

    ValueTask<RegistrationDecision> DecideAsync(RegistrationRequest request, CancellationToken ct);
}

/// <summary>
/// An account about to be made: the name, motto and figure as they would be saved (trimmed, the
/// gender's default figure when none was chosen), and who asks from which address (empty when not
/// known). The address is personal data: decide on it, don't log it.
/// </summary>
public sealed record RegistrationRequest(
    string Name,
    string Motto,
    string Figure,
    AvatarGenderType Gender,
    RegistrationSource Source,
    string RemoteAddress
);

/// <summary>A policy's answer: allowed, or refused with the reason the one asking is shown.</summary>
public sealed record RegistrationDecision(bool Allowed, string? Reason = null)
{
    public static readonly RegistrationDecision Allow = new(true);

    public static RegistrationDecision Refuse(string reason) => new(false, reason);
}
