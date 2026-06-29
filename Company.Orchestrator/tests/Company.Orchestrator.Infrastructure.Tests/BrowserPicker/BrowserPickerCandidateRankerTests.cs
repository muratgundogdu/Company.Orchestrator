using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Infrastructure.BrowserPicker;
using Xunit;

namespace Company.Orchestrator.Infrastructure.Tests.BrowserPicker;

public sealed class BrowserPickerCandidateRankerTests
{
  [Fact]
  public void PickPrimary_PrefersTableRowXPathOverDomPath()
  {
    var candidates = new List<BrowserPickerCandidate>
    {
      new()
      {
        Selector   = "#data > div:nth-of-type(1) > table > tbody > tr:nth-of-type(1) > td:nth-of-type(5)",
        Type       = "dom-path",
        Strategy   = "path",
        Confidence = "low",
        MatchCount = 1,
        Reason     = "DOM nth-of-type path (fragile fallback)",
      },
      new()
      {
        Selector   = "xpath=//*[@id='data']//tr[td[contains(normalize-space(.),'USD')]]/td[5]",
        Type       = "table-relative",
        Strategy   = "table-row-xpath",
        Confidence = "high",
        MatchCount = 1,
        Reason     = "Row-relative XPath by currency code USD (column 5) scoped to #data",
      },
    };

    var primary = BrowserPickerCandidateRanker.PickPrimary(candidates);

    Assert.NotNull(primary);
    Assert.Equal("table-row-xpath", primary.Strategy);
    Assert.Equal("high", primary.Confidence);
    Assert.StartsWith("xpath=//*[@id='data']//tr[td[contains(normalize-space(.),'USD')]]", primary.Selector);
  }

  [Fact]
  public void PickPrimary_WorksForEurRow()
  {
    var candidates = new List<BrowserPickerCandidate>
    {
      new()
      {
        Selector   = "table > tbody > tr:nth-of-type(3) > td:nth-of-type(5)",
        Type       = "dom-path",
        Strategy   = "path",
        Confidence = "low",
        MatchCount = 1,
        Reason     = "DOM path",
      },
      new()
      {
        Selector   = "xpath=//*[@id='data']//tr[td[contains(normalize-space(.),'EUR')]]/td[5]",
        Type       = "table-relative",
        Strategy   = "table-row-xpath",
        Confidence = "high",
        MatchCount = 1,
        Reason     = "Row-relative XPath by currency code EUR (column 5) scoped to #data",
      },
    };

    var primary = BrowserPickerCandidateRanker.PickPrimary(candidates);

    Assert.NotNull(primary);
    Assert.Contains("EUR", primary.Selector);
    Assert.Equal("high", primary.Confidence);
  }

  [Fact]
  public void PickPrimary_PrefersUniqueMatchOverAmbiguousTableXPath()
  {
    var candidates = new List<BrowserPickerCandidate>
    {
      new()
      {
        Selector   = "xpath=//tr[td[contains(normalize-space(.),'USD')]]/td[5]",
        Type       = "table-relative",
        Strategy   = "table-row-xpath",
        Confidence = "medium",
        MatchCount = 2,
        Reason     = "Ambiguous document-wide XPath",
      },
      new()
      {
        Selector   = "xpath=//*[@id='data']//tr[td[contains(normalize-space(.),'USD')]]/td[5]",
        Type       = "table-relative",
        Strategy   = "table-row-xpath",
        Confidence = "high",
        MatchCount = 1,
        Reason     = "Scoped XPath",
      },
    };

    var primary = BrowserPickerCandidateRanker.PickPrimary(candidates);

    Assert.NotNull(primary);
    Assert.Equal(1, primary.MatchCount);
    Assert.Equal("high", primary.Confidence);
    Assert.Contains("@id='data'", primary.Selector);
  }
}
