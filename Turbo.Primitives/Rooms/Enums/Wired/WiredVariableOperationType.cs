namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Operations of the "change variable value" action. Codes are the ids the client offers in its
/// operation dropdown (wiredfurni.params.variables.operation.&lt;id&gt;). The basic block is 0..6, the
/// advanced block starts at 40. <see cref="Absolute"/>, <see cref="Invert"/> and
/// <see cref="BitCount"/> take no operand (AS3 <c>ChangeVariable.requiresOperand</c>). The meaning of
/// 111 to 122 is not confirmed: the editor offers them after Bit count, and the official bitwise
/// tutorial lists next / previous low / high bit and get / set / clear / toggle bit there.
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
    IsEqual = 111,
    IsNotEqual = 112,
    IsLess = 113,
    IsGreater = 114,
    SetIfLess = 115,
    SetIfGreater = 116,
    AddClampedToOperand = 117,
    SubtractClampedToOperand = 118,
    Average = 119,
    Distance = 120,
    IsLessOrEqual = 121,
    IsGreaterOrEqual = 122,
}
