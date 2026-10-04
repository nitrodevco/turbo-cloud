namespace Turbo.Admin.Links;

/// <summary>Why a setup link was not made.</summary>
public enum AdminLinkRefusal
{
    /// <summary>It was made.</summary>
    None,

    /// <summary>
    /// A player asked for their own link but already has a passkey. There is no recovery by
    /// oneself: a lost passkey is replaced through an admin.
    /// </summary>
    AlreadySetUp,

    /// <summary>A link for somebody else needs <c>admin.passkeys.reset</c>.</summary>
    NeedsResetNode,

    /// <summary>
    /// The player holds a node the issuer does not. A link is the account, so it would hand the
    /// issuer more than they have.
    /// </summary>
    OutranksIssuer,
}
