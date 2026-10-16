namespace Turbo.Primitives.Catalog.Enums;

/// <summary>
/// The answer to <c>ClaimProduct</c> (<c>ClaimProductResult.result</c>): the client's
/// <c>claim_product.result.&lt;n&gt;</c> texts name each, and its adoption window
/// (<c>CursedDragonAdoptionController.onClaimProductResult</c>) reads 0 and 1 as claimed, 2 and 3
/// as unavailable and anything else as "try again".
/// </summary>
public enum ProductClaimResult
{
    Claimed = 0,
    AlreadyClaimed = 1,
    NotFound = 2,
    NotActive = 3,
    Failed = 4,
}
