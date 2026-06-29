using Company.Orchestrator.Infrastructure.ExcelTransform;
using Xunit;

namespace Company.Orchestrator.Infrastructure.Tests.ExcelTransform;

public sealed class ExcelTransformColumnExpressionEvaluatorTests
{
  private static ExcelTransformExpressionContext Ctx(
      string value,
      Dictionary<string, string>? row = null,
      Dictionary<string, object>? variables = null) =>
      new()
      {
          Value     = value,
          Row       = row ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
          Variables = variables ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
      };

  [Fact]
  public void Evaluate_LegacyValueExpression_StillWorks()
  {
    var result = ExcelTransformColumnExpressionEvaluator.Evaluate(
        "toNumber(value) / 100",
        Ctx("250"));

    Assert.Equal(2.5, Convert.ToDouble(result));
  }

  [Fact]
  public void Evaluate_VariableMultiplication_Works()
  {
    var result = ExcelTransformColumnExpressionEvaluator.Evaluate(
        "toNumber(value) * toNumber(variables.usdRateText)",
        Ctx("10", variables: new Dictionary<string, object>
        {
            ["usdRateText"] = "46.6390",
        }));

    Assert.Equal(466.39, Convert.ToDouble(result), 3);
  }

  [Fact]
  public void Evaluate_RowColumnCalculation_Works()
  {
    var result = ExcelTransformColumnExpressionEvaluator.Evaluate(
        "toNumber(row[\"Fiyat\"]) * toNumber(variables.usdRateText)",
        Ctx("ignored", row: new Dictionary<string, string>
        {
            ["Fiyat"] = "100",
        }, variables: new Dictionary<string, object>
        {
            ["usdRateText"] = "2",
        }));

    Assert.Equal(200.0, Convert.ToDouble(result));
  }

  [Theory]
  [InlineData("46.6390")]
  [InlineData("46,6390")]
  public void ToNumber_ParsesTurkishAndInvariantDecimals(string input)
  {
    var result = ExcelTransformColumnExpressionEvaluator.ToNumber(input);
    Assert.Equal(46.6390, result, 4);
  }

  [Fact]
  public void Evaluate_RowColumnByLetter_Works()
  {
    var result = ExcelTransformColumnExpressionEvaluator.Evaluate(
        "row[\"Urun\"]",
        Ctx("x", row: new Dictionary<string, string>
        {
            ["Urun"] = "Laptop",
        }));

    Assert.Equal("Laptop", result);
  }

  [Fact]
  public void Evaluate_RowColumnByExcelLetter_Works()
  {
    var result = ExcelTransformColumnExpressionEvaluator.Evaluate(
        "toNumber(row[\"B\"]) * 2",
        Ctx("ignored", row: new Dictionary<string, string>
        {
            ["B"] = "50",
        }));

    Assert.Equal(100.0, Convert.ToDouble(result));
  }

  [Fact]
  public void Evaluate_TrimAndRemoveLeadingZeros_StillWork()
  {
    var trimResult = ExcelTransformColumnExpressionEvaluator.Evaluate("trim(value)", Ctx("  abc  "));
    var zerosResult = ExcelTransformColumnExpressionEvaluator.Evaluate("removeLeadingZeros(value)", Ctx("00042"));

    Assert.Equal("abc", trimResult);
    Assert.Equal("42", zerosResult);
  }

  [Fact]
  public void Evaluate_BackwardCompatibleOverload_UsesValueOnly()
  {
    var result = ExcelTransformColumnExpressionEvaluator.Evaluate("toNumber(value) / 100", "300");
    Assert.Equal(3.0, Convert.ToDouble(result));
  }
}
