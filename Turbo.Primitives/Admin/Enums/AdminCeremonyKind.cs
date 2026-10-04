namespace Turbo.Primitives.Admin.Enums;

/// <summary>What a passkey ceremony is for, so one started for one thing cannot finish another.</summary>
public enum AdminCeremonyKind
{
    /// <summary>Signing in, or confirming it is still the signed-in player, with a passkey.</summary>
    SignIn,

    /// <summary>Registering a new passkey, at setup or from the account page.</summary>
    Register,
}
