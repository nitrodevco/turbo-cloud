using System.Collections.Generic;

namespace Turbo.Catalog.Configuration;

/// <summary>
/// The free products a player can claim once each, by claim id: the id the client asks about
/// (<c>HasClaimedProduct</c>) and claims (<c>ClaimProduct</c>). Which claims the client offers is the
/// hotel's client configuration (<c>cursed_dragon_adoption.claim_id</c>,
/// <c>special_items.&lt;key&gt;.free_claim</c>, <c>notifications.new_feature.auto_open_condition.&lt;key&gt;</c>).
/// </summary>
public sealed class ProductClaimConfig
{
    public const string SECTION_NAME = "Turbo:ProductClaims";

    public Dictionary<string, ProductClaimDefinition> Claims { get; set; } = [];
}

public sealed class ProductClaimDefinition
{
    /// <summary>When off, the claim answers "not active" and gives nothing.</summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// What the claim gives, by the client's option index (the adoption window's blue button sends
    /// 0, the purple one 1). A claim with a single choice may be sent without an index.
    /// </summary>
    public List<ProductClaimChoice> Choices { get; set; } = [];
}

public sealed class ProductClaimChoice
{
    /// <summary>The pet given, as Habbo writes a pet figure: "type palette colour" ("37 0 ffffff").</summary>
    public string Pet { get; set; } = string.Empty;

    /// <summary>The name the pet is given; empty for <see cref="DefaultPetName"/>.</summary>
    public string PetName { get; set; } = string.Empty;

    public const string DefaultPetName = "Pet";
}
