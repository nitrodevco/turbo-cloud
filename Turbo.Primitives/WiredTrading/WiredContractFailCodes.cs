namespace Turbo.Primitives.WiredTrading;

/// <summary>
/// Why saving a wired contract failed. The client shows the text key
/// <c>wiredcontracts.error.&lt;code&gt;</c>, falling back to the key itself, so each code is
/// the suffix of a key in ExternalTexts.
/// </summary>
public static class WiredContractFailCodes
{
    /// <summary>"Invalid or empty requirements".</summary>
    public const string INVALID_RULES = "invalid_rules";
}
