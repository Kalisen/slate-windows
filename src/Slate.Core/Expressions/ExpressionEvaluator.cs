using System.Globalization;

namespace Slate.Core.Expressions;

public sealed class ExpressionException(string message) : Exception(message);

/// <summary>
/// Evaluates Slate arithmetic expressions such as <c>screenOriginX+screenSizeX/2</c>.
/// Supports + - * / ** (right associative), unary minus, parentheses, variables, array literals <c>{1,2,3}</c>
/// and the functions listed in <see cref="ExpressionFunction"/>.
/// </summary>
public static class ExpressionEvaluator
{
    public static double Evaluate(string expression, IReadOnlyDictionary<string, double> variables, Random? random = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ExpressionException("Expression is empty");
        var parser = new Parser(expression, variables, random ?? Random.Shared);
        double value = parser.ParseExpression().Scalar(expression);
        parser.ExpectEnd();
        return value;
    }

    /// <summary>A parsed value: either a number or an array literal (only valid as a function argument).</summary>
    private readonly record struct Value(double Number, IReadOnlyList<double>? Array)
    {
        public double Scalar(string expression) =>
            Array is null ? Number : throw new ExpressionException($"Unexpected array in '{expression}'");
    }

    private sealed class Parser(string text, IReadOnlyDictionary<string, double> variables, Random random)
    {
        private int pos;

        public void ExpectEnd()
        {
            SkipSpace();
            if (pos < text.Length) throw Error($"Unexpected '{text[pos]}'");
        }

        public Value ParseExpression()
        {
            double value = ParseTerm();
            while (true)
            {
                if (Accept('+')) value += ParseTerm();
                else if (Accept('-')) value -= ParseTerm();
                else return new Value(value, null);
            }
        }

        private double ParseTerm()
        {
            double value = ParsePower();
            while (true)
            {
                if (Accept('*')) value *= ParsePower();
                else if (Accept('/')) value /= ParsePower();
                else return value;
            }
        }

        private double ParsePower()
        {
            double baseValue = ParseUnary();
            SkipSpace();
            if (Peek("**"))
            {
                pos += 2;
                return Math.Pow(baseValue, ParsePower());
            }
            return baseValue;
        }

        private double ParseUnary()
        {
            if (Accept('-')) return -ParseUnary();
            if (Accept('+')) return ParseUnary();
            return ParsePrimary().Scalar(text);
        }

        private Value ParsePrimary()
        {
            SkipSpace();
            if (pos >= text.Length) throw Error("Unexpected end of expression");
            char c = text[pos];
            if (Accept('('))
            {
                Value inner = ParseExpression();
                Expect(')');
                return inner;
            }
            if (Accept('{'))
            {
                var items = new List<double>();
                if (!Accept('}'))
                {
                    do { items.Add(ParseExpression().Scalar(text)); } while (Accept(','));
                    Expect('}');
                }
                return new Value(0, items);
            }
            if (char.IsDigit(c) || c == '.') return new Value(ParseNumber(), null);
            if (char.IsLetter(c) || c == '_') return ParseIdentifier();
            throw Error($"Unexpected '{c}'");
        }

        private double ParseNumber()
        {
            int start = pos;
            while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '.')) pos++;
            string literal = text[start..pos];
            if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw Error($"Invalid number '{literal}'");
            return value;
        }

        private Value ParseIdentifier()
        {
            int start = pos;
            while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_')) pos++;
            string name = text[start..pos];
            SkipSpace();
            if (Accept('('))
            {
                var args = new List<double>();
                if (!Accept(')'))
                {
                    do
                    {
                        // An array literal is only valid as a whole argument, e.g. sum({1,2,3}).
                        Value arg = Peek("{") ? ParsePrimary() : ParseExpression();
                        if (arg.Array is null) args.Add(arg.Number); else args.AddRange(arg.Array);
                    } while (Accept(','));
                    Expect(')');
                }
                return new Value(ExpressionFunctions.Call(name, args, random, text), null);
            }
            if (variables.TryGetValue(name, out double value)) return new Value(value, null);
            throw Error($"Unknown variable '{name}'");
        }

        private void SkipSpace()
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        }

        private bool Peek(string token)
        {
            SkipSpace();
            return string.CompareOrdinal(text, pos, token, 0, token.Length) == 0;
        }

        private bool Accept(char c)
        {
            SkipSpace();
            if (pos < text.Length && text[pos] == c && !(c == '*' && Peek("**")))
            {
                pos++;
                return true;
            }
            return false;
        }

        private void Expect(char c)
        {
            if (!Accept(c)) throw Error($"Expected '{c}'");
        }

        private ExpressionException Error(string reason) => new($"{reason} in '{text}'");
    }
}
