namespace Turbo.Primitives.Inventory;

/// <summary>Why furniture changed owner, as <c>furniture_transfers</c> records it.</summary>
public enum FurnitureTransferReason : byte
{
    /// <summary>One side of a completed trade.</summary>
    Trade = 0,

    /// <summary>A trade's first side handed back because its second side could not move.</summary>
    TradeUndone = 1,
}
