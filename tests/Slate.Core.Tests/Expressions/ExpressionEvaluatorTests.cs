using Slate.Core.Expressions;

namespace Slate.Core.Tests.Expressions;

public class ExpressionEvaluatorTests
{
    private static readonly Dictionary<string, double> Vars = new() { ["var1"] = 5, ["var2"] = 10, ["var3"] = 2 };

    [Theory]
    [InlineData("var1+var2", 15)]
    [InlineData("var1*var2", 50)]
    [InlineData("var2-var1-var3", 3)]
    [InlineData("var2/var3/var1", 1)]
    [InlineData("1+2*3", 7)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("-var1+2", -3)]
    [InlineData("2**3**2", 512)]
    [InlineData("3/2", 1.5)]
    [InlineData("sqrt((var2/var3)*var1)+count({1,2,3})", 8)]
    [InlineData("var3**var2+sum({1,2,3})", 1030)]
    [InlineData("min({1,2,3})+max({4,5,6})", 7)]
    [InlineData("min(4,2)", 2)]
    [InlineData("average({1,2,3,4})+median({1,2,3,10,15})", 5.5)]
    [InlineData("median({1,2,3,4})", 2.5)]
    [InlineData("floor(3.9)+(stddev({1,2,3,4,5}))**2", 5)]
    [InlineData("log(1000)+ln(exp(5))", 8)]
    [InlineData("ceiling(2.1)+abs(-2)", 5)]
    [InlineData("trunc(3.232472398472834723)", 3)]
    [InlineData("screenSizeX/2", 960)]
    public void Evaluate(string expression, double expected)
    {
        var vars = new Dictionary<string, double>(Vars) { ["screenSizeX"] = 1920 };
        Assert.Equal(expected, ExpressionEvaluator.Evaluate(expression, vars), precision: 9);
    }

    [Fact]
    public void Random_functions_stay_in_range()
    {
        double r = ExpressionEvaluator.Evaluate("random()", Vars);
        double n = ExpressionEvaluator.Evaluate("randomn(10)", Vars);
        Assert.InRange(r, 0, 0.999999);
        Assert.InRange(n, 0, 9);
        Assert.Equal(Math.Floor(n), n);
    }

    [Theory]
    [InlineData("unknownVar+1")]
    [InlineData("1+")]
    [InlineData("nope(1)")]
    [InlineData("(1+2")]
    [InlineData("{1,2}")]
    [InlineData("1 2")]
    [InlineData("")]
    public void Invalid_expressions_throw(string expression) =>
        Assert.Throws<ExpressionException>(() => ExpressionEvaluator.Evaluate(expression, Vars));

    [Theory]
    [InlineData("1;2", "1", "2")]
    [InlineData("1,2", "1", "2")]
    [InlineData("min({1,2});3", "min({1,2})", "3")]
    public void ExpressionPoint_parses_pairs(string text, string x, string y) =>
        Assert.Equal(new ExpressionPoint(x, y), ExpressionPoint.Parse(text));

    [Fact]
    public void ExpressionPoint_rejects_single_values() => Assert.Null(ExpressionPoint.Parse("100"));
}
