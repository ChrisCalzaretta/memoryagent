using Xunit;
using DesignAgent.Server.Models.DesignIntelligence;

namespace DesignAgent.Tests.Models;

public class DesignSourceTests
{
    [Fact]
    public void DesignSource_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var source = new DesignSource
        {
            Url = "https://linear.app"
        };

        // Assert
        Assert.NotNull(source.Id);
        Assert.False(string.IsNullOrEmpty(source.Id));
        Assert.Equal("https://linear.app", source.Url);
        Assert.Equal(5.0, source.TrustScore); // Default
        Assert.Equal("pending", source.Status); // Default
        Assert.False(source.AlreadyEvaluated);
        Assert.Empty(source.Tags);
    }

    [Fact]
    public void DesignSource_ShouldAllowTrustScoreOverride()
    {
        // Arrange & Act
        var source = new DesignSource
        {
            Url = "https://linear.app",
            TrustScore = 10.0
        };

        // Assert
        Assert.Equal(10.0, source.TrustScore);
    }
}

public class CapturedDesignTests
{
    [Fact]
    public void CapturedDesign_ShouldInitializeWithEmptyPages()
    {
        // Arrange & Act
        var design = new CapturedDesign
        {
            Url = "https://linear.app"
        };

        // Assert
        Assert.NotNull(design.Pages);
        Assert.Empty(design.Pages);
        Assert.Equal(0, design.OverallScore);
        Assert.False(design.PassedQualityGate);
    }

    [Fact]
    public void CapturedDesign_ShouldAllowMultiplePages()
    {
        // Arrange
        var design = new CapturedDesign
        {
            Url = "https://linear.app"
        };

        // Act
        design.Pages.Add(new PageAnalysis
        {
            DesignId = design.Id,
            Url = "https://linear.app",
            PageType = "homepage",
            OverallPageScore = 9.2
        });
        design.Pages.Add(new PageAnalysis
        {
            DesignId = design.Id,
            Url = "https://linear.app/pricing",
            PageType = "pricing",
            OverallPageScore = 9.0
        });

        // Assert
        Assert.Equal(2, design.Pages.Count);
        Assert.Contains(design.Pages, p => p.PageType == "homepage");
        Assert.Contains(design.Pages, p => p.PageType == "pricing");
    }
}

public class PageAnalysisTests
{
    [Fact]
    public void PageAnalysis_ShouldInitializeWithEmptyCollections()
    {
        // Arrange & Act
        var page = new PageAnalysis
        {
            DesignId = Guid.NewGuid().ToString(),
            Url = "https://linear.app",
            PageType = "homepage"
        };

        // Assert
        Assert.Empty(page.CategoryScores);
        Assert.Empty(page.Strengths);
        Assert.Empty(page.Weaknesses);
        Assert.Equal(1.0, page.PageWeight); // Default
    }

    [Fact]
    public void PageAnalysis_ShouldAllowCategoryScores()
    {
        // Arrange
        var page = new PageAnalysis
        {
            DesignId = Guid.NewGuid().ToString(),
            Url = "https://linear.app",
            PageType = "homepage"
        };

        // Act
        page.CategoryScores["hero"] = 9.2;
        page.CategoryScores["nav"] = 8.5;
        page.CategoryScores["socialProof"] = 9.0;

        // Assert
        Assert.Equal(3, page.CategoryScores.Count);
        Assert.Equal(9.2, page.CategoryScores["hero"]);
    }
}

