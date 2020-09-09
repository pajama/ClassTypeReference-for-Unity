namespace TypeReferences.Editor.Odin
{
    using System;

    internal static class FloatExtensions
    {
        private const float Tolerance = 0.01f;

        public static bool ApproximatelyEquals(this float firstNum, float secondNum)
        {
            return Math.Abs(firstNum - secondNum) < Tolerance;
        }

        public static bool DoesNotEqualApproximately(this float firstNum, float secondNum)
        {
            return ! firstNum.ApproximatelyEquals(secondNum);
        }
    }
}