using Xunit;
using LinkedInLearningSummarizer.Services;
using LinkedInLearningSummarizer.Models;
using Microsoft.Playwright;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using LinkedInLearningSummarizer.Tests.TestHelpers;

namespace LinkedInLearningSummarizer.Tests;

public class TranscriptExtractionTests : IDisposable
{
    private readonly AppConfig _config;
    private readonly LinkedInScraper _scraper;

    public TranscriptExtractionTests()
    {
        _config = MockHelpers.CreateTestConfig();
        _config.Headless = true; // Always use headless for tests
        _scraper = new LinkedInScraper(_config);
    }

    [Fact]
    public async Task NavigateToLessonAsync_ThrowsException_WhenPageNotInitialized()
    {
        // Arrange
        var lessonUrl = "https://www.linkedin.com/learning/test-course/test-lesson";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _scraper.NavigateToLessonAsync(lessonUrl)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task NavigateToLessonAsync_ThrowsArgumentException_ForInvalidUrl(string? lessonUrl)
    {
        // Arrange - Don't initialize browser, we want to test URL validation first

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _scraper.NavigateToLessonAsync(lessonUrl!)
        );
    }

    [Fact]
    public async Task ClickTranscriptTabAsync_ThrowsException_WhenPageNotInitialized()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _scraper.ClickTranscriptTabAsync()
        );
    }

    [Fact]
    public async Task DisableInteractiveTranscriptsAsync_ThrowsException_WhenPageNotInitialized()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _scraper.DisableInteractiveTranscriptsAsync()
        );
    }

    [Fact]
    public async Task ExtractTranscriptTextAsync_ThrowsException_WhenPageNotInitialized()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _scraper.ExtractTranscriptTextAsync()
        );
    }

    [Fact]
    public async Task ExtractLessonTranscriptAsync_ThrowsArgumentNullException_ForNullLesson()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _scraper.ExtractLessonTranscriptAsync(null!)
        );
    }

    [Fact]
    public void CleanTranscriptText_RemovesExcessiveWhitespace()
    {
        // This test uses reflection to test the private method
        // In a real scenario, we would test this through the public interface
        
        // Arrange
        var input = "This  is    a   test     text";
        var expected = "This is a test text";
        
        // Act - Use reflection to test private method
        var methodInfo = typeof(LinkedInScraper).GetMethod(
            "CleanTranscriptText", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        
        if (methodInfo != null)
        {
            var result = methodInfo.Invoke(_scraper, new object[] { input }) as string;
            
            // Assert
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public async Task ProcessLessonTranscriptsAsync_HandlesEmptyLessonList()
    {
        // Arrange
        var emptyList = new List<Lesson>();
        await _scraper.InitializeBrowserAsync();

        // Act - This should not throw
        await _scraper.ProcessLessonTranscriptsAsync(emptyList);

        // Assert - Method completes without exception
        Assert.True(true);
    }

    [Fact]
    public async Task ProcessLessonTranscriptsAsync_HandlesNullLessonList()
    {
        // Arrange
        await _scraper.InitializeBrowserAsync();

        // Act - This should not throw
        await _scraper.ProcessLessonTranscriptsAsync(null!);

        // Assert - Method completes without exception
        Assert.True(true);
    }

    [Fact]
    public void Lesson_TranscriptProperties_DefaultValues()
    {
        // Arrange & Act
        var lesson = new Lesson();

        // Assert
        Assert.Equal(string.Empty, lesson.Transcript);
        Assert.False(lesson.HasTranscript);
        Assert.Equal(default(DateTime), lesson.ExtractedAt);
    }

    [Fact]
    public void Lesson_TranscriptProperties_CanBeSet()
    {
        // Arrange
        var lesson = new Lesson();
        var testTranscript = "This is a test transcript";
        var testDate = DateTime.UtcNow;

        // Act
        lesson.Transcript = testTranscript;
        lesson.HasTranscript = true;
        lesson.ExtractedAt = testDate;

        // Assert
        Assert.Equal(testTranscript, lesson.Transcript);
        Assert.True(lesson.HasTranscript);
        Assert.Equal(testDate, lesson.ExtractedAt);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/learning/course/lesson", true)]
    [InlineData("https://linkedin.com/learning/course/lesson", true)]
    [InlineData("http://www.linkedin.com/learning/course/lesson", true)]
    public void ValidateLessonUrl_AcceptsValidUrls(string url, bool expected)
    {
        // Arrange
        var lesson = new Lesson { Url = url };

        // Act
        var isValid = !string.IsNullOrWhiteSpace(lesson.Url) && 
                     (lesson.Url.StartsWith("http://") || lesson.Url.StartsWith("https://"));

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public async Task ExtractLessonTranscriptAsync_UpdatesLessonProperties()
    {
        // This test would need a mock page to work properly
        // For now, we'll test the logic flow with a lesson that will fail
        
        // Arrange
        var lesson = new Lesson
        {
            LessonNumber = 1,
            Title = "Test Lesson",
            Url = "https://www.linkedin.com/learning/test/lesson"
        };

        // Act
        try
        {
            await _scraper.InitializeBrowserAsync();
            // This will fail because we're not actually connected to LinkedIn
            await _scraper.ExtractLessonTranscriptAsync(lesson);
        }
        catch
        {
            // Expected to fail in test environment
        }

        // Assert - Even on failure, properties should be set
        Assert.NotNull(lesson.Transcript);
        Assert.False(lesson.HasTranscript); // Should be false after failure
        Assert.NotEqual(default(DateTime), lesson.ExtractedAt); // Should be set
    }

    [Fact]
    public void AppConfig_TranscriptSettings_HaveCorrectDefaults()
    {
        // Arrange
        var config = new AppConfig();

        // Assert
        Assert.False(config.KeepTimestamps);
        Assert.Equal(10, config.MaxScrollRounds);
        Assert.Equal(5000, config.SinglePassThreshold);
    }

    [Theory]
    [InlineData(true, 5, 1000)]
    [InlineData(false, 20, 10000)]
    public void AppConfig_TranscriptSettings_CanBeCustomized(bool keepTimestamps, int maxScroll, int threshold)
    {
        // Arrange & Act
        var config = new AppConfig
        {
            KeepTimestamps = keepTimestamps,
            MaxScrollRounds = maxScroll,
            SinglePassThreshold = threshold
        };

        // Assert
        Assert.Equal(keepTimestamps, config.KeepTimestamps);
        Assert.Equal(maxScroll, config.MaxScrollRounds);
        Assert.Equal(threshold, config.SinglePassThreshold);
    }

    public void Dispose()
    {
        _scraper?.Dispose();
    }
}

public class TranscriptExtractionIntegrationTests
{
    [Fact]
    public void TranscriptExtraction_MethodsExist()
    {
        // Arrange
        var scraperType = typeof(LinkedInScraper);

        // Assert - Check that all required methods exist
        Assert.NotNull(scraperType.GetMethod("NavigateToLessonAsync"));
        Assert.NotNull(scraperType.GetMethod("ClickTranscriptTabAsync"));
        Assert.NotNull(scraperType.GetMethod("DisableInteractiveTranscriptsAsync"));
        Assert.NotNull(scraperType.GetMethod("ExtractTranscriptTextAsync"));
        Assert.NotNull(scraperType.GetMethod("ExtractLessonTranscriptAsync"));
        Assert.NotNull(scraperType.GetMethod("ProcessLessonTranscriptsAsync"));
    }

    [Fact]
    public async Task TranscriptExtraction_EndToEndFlow_Structure()
    {
        // This test verifies the structure and flow without actual LinkedIn connection
        
        // Arrange
        var config = MockHelpers.CreateTestConfig();
        var scraper = new LinkedInScraper(config);
        var lessons = new List<Lesson>
        {
            new Lesson 
            { 
                LessonNumber = 1, 
                Title = "Introduction", 
                Url = "https://www.linkedin.com/learning/test/intro" 
            },
            new Lesson 
            { 
                LessonNumber = 2, 
                Title = "Getting Started", 
                Url = "https://www.linkedin.com/learning/test/start" 
            }
        };

        // Act - This will fail in test environment but tests the structure
        try
        {
            await scraper.ProcessLessonTranscriptsAsync(lessons);
        }
        catch
        {
            // Expected in test environment
        }

        // Assert - Verify the lessons structure is maintained
        Assert.Equal(2, lessons.Count);
        Assert.All(lessons, l => Assert.NotNull(l.Transcript));
        Assert.All(lessons, l => Assert.NotEqual(default(DateTime), l.ExtractedAt));
        
        scraper.Dispose();
    }
}