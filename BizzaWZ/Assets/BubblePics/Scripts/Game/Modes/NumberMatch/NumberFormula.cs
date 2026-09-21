using System;
using System.Globalization;

namespace BubblePics
{
    /// <summary>
    /// Parser/evaluator for the deliberately small formula language used by
    /// the 1.0.9 Number Match bank: a bare integer, A+B, or A-B.
    /// </summary>
    public static class NumberFormula
    {
        public const int MinOperand = 1;
        public const int MaxOperand = 30;

        public readonly struct Parsed
        {
            public readonly int A;
            public readonly int B;
            public readonly char Operator;
            public readonly int Result;
            public readonly bool IsBare;

            public Parsed(int a, int b, char operation, int result, bool isBare)
            {
                A = a;
                B = b;
                Operator = operation;
                Result = result;
                IsBare = isBare;
            }
        }

        public static bool TryParse(string formula, out Parsed parsed, out string error)
        {
            parsed = default;
            error = string.Empty;
            string value = formula == null ? string.Empty : formula.Trim();
            if (value.Length == 0)
            {
                error = "empty formula";
                return false;
            }

            int operatorIndex = -1;
            char operation = '\0';
            for (int i = 1; i < value.Length; i++)
            {
                char candidate = value[i];
                if (candidate != '+' && candidate != '-') continue;
                operatorIndex = i;
                operation = candidate;
                break;
            }

            if (operatorIndex < 0)
            {
                if (!TryInteger(value, out int bare))
                {
                    error = $"not an integer and has no +/- operator in '{value}'";
                    return false;
                }

                parsed = new Parsed(bare, 0, '\0', bare, true);
                return true;
            }

            string left = value.Substring(0, operatorIndex).Trim();
            string right = value.Substring(operatorIndex + 1).Trim();
            if (!TryInteger(left, out int a) || !TryInteger(right, out int b))
            {
                error = $"non-integer operand in '{value}'";
                return false;
            }

            int result = operation == '+' ? a + b : a - b;
            parsed = new Parsed(a, b, operation, result, false);
            return true;
        }

        public static bool TryValidate(string formula, out int result, out string error)
        {
            result = 0;
            if (!TryParse(formula, out Parsed parsed, out error)) return false;

            if (parsed.IsBare)
            {
                if (!InOperandRange(parsed.Result))
                {
                    error = $"bare number {parsed.Result} out of [{MinOperand},{MaxOperand}]";
                    return false;
                }
            }
            else if (!InOperandRange(parsed.A) || !InOperandRange(parsed.B))
            {
                error = $"operand out of [{MinOperand},{MaxOperand}] in '{formula}'";
                return false;
            }

            result = parsed.Result;
            error = string.Empty;
            return true;
        }

        public static int Evaluate(string formula)
        {
            return TryParse(formula, out Parsed parsed, out _) ? parsed.Result : 0;
        }

        static bool TryInteger(string value, out int result)
        {
            return int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out result);
        }

        static bool InOperandRange(int value)
        {
            return value >= MinOperand && value <= MaxOperand;
        }
    }
}
