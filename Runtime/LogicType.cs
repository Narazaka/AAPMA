namespace Narazaka.Unity.AAPMA
{
    public enum LogicType
    {
        [IString("Remap", "範囲変換")]
        Remap,
        [IString("Addition(+)", "加算(＋)")]
        Addition,
        [IString("Subtraction(-)", "減算(－)")]
        Subtraction,
        [IString("Multiplication(*)", "乗算(×)")]
        Multiplication,
        [IString("Division( ∕ )", "除算(÷)")]
        Division,
        [IString("Exponential Smoothing", "指数スムージング")]
        ExponentialSmoothing,
        [IString("Linear Smoothing", "線形スムージング")]
        LinearSmoothing,
        [IString("Arbitrary 2-Bit Gate", "任意 2-bit ゲート")]
        Arbitrary2Bit,
        [IString("AND Gate", "AND ゲート")]
        And,
        [IString("OR Gate", "OR ゲート")]
        Or,
        [IString("NOT Gate", "NOT ゲート")]
        Not,
    }
}
