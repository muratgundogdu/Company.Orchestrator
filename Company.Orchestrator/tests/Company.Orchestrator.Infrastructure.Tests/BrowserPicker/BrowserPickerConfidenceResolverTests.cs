using Company.Orchestrator.Infrastructure.BrowserPicker;
using Xunit;

namespace Company.Orchestrator.Infrastructure.Tests.BrowserPicker;

public sealed class BrowserPickerConfidenceResolverTests
{
  [Theory]
  [InlineData(1, "high")]
  [InlineData(2, "medium")]
  [InlineData(3, "medium")]
  [InlineData(4, "low")]
  [InlineData(10, "low")]
  public void FromMatchCount_FollowsHealingRules(int matchCount, string expected)
  {
    Assert.Equal(expected, BrowserPickerConfidenceResolver.FromMatchCount(matchCount));
  }

  [Fact]
  public void FromMatchCount_NeverHighWhenMultipleMatches()
  {
    foreach (var count in new[] { 2, 3, 4, 5, 12 })
    {
      Assert.NotEqual("high", BrowserPickerConfidenceResolver.FromMatchCount(count));
    }
  }
}
