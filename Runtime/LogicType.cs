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
        [IString("Exponential Smoothing", "指数平滑化")]
        ExponentialSmoothing,
        [IString("Linear Smoothing", "線形平滑化")]
        LinearSmoothing,
        /*
        And,
        Or,
        Not,
        Arbitrary2Bit,
        */
    }
}
