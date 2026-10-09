namespace Turbo.Database.Entities.Players;

/// <summary>
/// What a <see cref="HumanRespectParticipantReceiptEntity"/> records for its player. Each must
/// fit the receipt's <c>Kind</c> column: <c>pet-spend</c> once outgrew it, and every pet scratch
/// failed on MySQL while SQLite, which ignores column lengths, let the tests pass.
/// </summary>
public static class RespectReceiptKinds
{
    /// <summary>The player spent one of today's respects on another player.</summary>
    public const string SPEND = "spend";

    /// <summary>The player spent one of today's pet respects (a scratch).</summary>
    public const string PET_SPEND = "pet-spend";

    /// <summary>The player received a respect.</summary>
    public const string RECEIVE = "receive";
}
