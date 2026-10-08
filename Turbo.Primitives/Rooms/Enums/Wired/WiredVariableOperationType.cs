namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Operations of the "change variable value" action. Codes are the ids the client offers in its
/// operation dropdown (wiredfurni.params.variables.operation.&lt;id&gt;). The basic block is 0..6, the
/// advanced block starts at 40. <see cref="Absolute"/>, <see cref="Invert"/> and
/// <see cref="BitCount"/> take no operand (AS3 <c>ChangeVariable.requiresOperand</c>). From 111 the
/// operand is a bit position (0 is the lowest bit of the 64-bit value).
/// </summary>
public enum WiredVariableOperationType
{
    Set = 0,
    Add = 1,
    Subtract = 2,
    Multiply = 3,
    Divide = 4,
    Power = 5,
    Modulo = 6,
    Minimum = 40,
    Maximum = 41,
    Random = 50,
    Absolute = 60,
    BitwiseAnd = 100,
    BitwiseOr = 101,
    BitwiseXor = 102,
    Invert = 103,
    ShiftLeft = 104,
    ShiftRight = 105,
    BitCount = 110,
    NextLowBitInclusive = 111,
    NextHighBitInclusive = 112,
    PreviousLowBitInclusive = 113,
    PreviousHighBitInclusive = 114,
    GetBit = 115,
    SetBit = 116,
    ClearBit = 117,
    ToggleBit = 118,
    NextLowBitExclusive = 119,
    NextHighBitExclusive = 120,
    PreviousLowBitExclusive = 121,
    PreviousHighBitExclusive = 122,
}
