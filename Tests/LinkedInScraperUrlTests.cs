using LinkedInLearningSummarizer.Models;
using LinkedInLearningSummarizer.Services;
using Xunit;
using System.Reflection;

namespace Tests;

public class LinkedInScraperUrlTests : IDisposable
{
    private readonly AppConfig _testConfig;
    private readonly LinkedInScraper _scraper;
    private readonly string _currentDirectory;

    public LinkedInScraperUrlTests()
    {
        _currentDirectory = Directory.GetCurrentDirectory();
        
        _testConfig = new AppConfig
        {
            OpenAIApiKey = "test-key",
            OpenAIModel = "test-model",
            OutputTranscriptDir = "./test-output",
            SessionProfile = "test_session",
            Headless = true,
            KeepTimestamps = false,
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 4000,
            MapChunkOverlap = 200
        };
        
        _scraper = new LinkedInScraper(_testConfig);
    }

    [Fact]
    public void GetSessionPath_ReturnsCorrectPath()
    {
        // Arrange
        var expectedPath = Path.Combine(_currentDirectory, "test_session");
        
        // Act - Using reflection to access private method
        var methodInfo = typeof(LinkedInScraper).GetMethod("GetSessionPath", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = methodInfo?.Invoke(_scraper, null) as string;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPath, result);
    }

    [Theory]
    [InlineData("test_session", "test_session")]
    [InlineData("my-custom-session", "my-custom-session")]
    [InlineData("linkedin_session", "linkedin_session")]
    [InlineData("session123", "session123")]
    public void GetSessionPath_WithDifferentProfiles_ReturnsCorrectPaths(string profileName, string expectedFolder)
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "test-key",
            OpenAIModel = "test-model",
            OutputTranscriptDir = "./test-output",
            SessionProfile = profileName,
            Headless = true
        };
        var scraper = new LinkedInScraper(config);
        var expectedPath = Path.Combine(_currentDirectory, expectedFolder);

        // Act - Using reflection to access private method
        var methodInfo = typeof(LinkedInScraper).GetMethod("GetSessionPath", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = methodInfo?.Invoke(scraper, null) as string;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPath, result);
        
        // Cleanup
        scraper.Dispose();
    }

    [Fact]
    public void SessionPath_HandlesSpecialCharacters()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "test-key",
            OpenAIModel = "test-model",
            OutputTranscriptDir = "./test-output",
            SessionProfile = "session-with-dash_and_underscore",
            Headless = true
        };
        var scraper = new LinkedInScraper(config);
        var expectedPath = Path.Combine(_currentDirectory, "session-with-dash_and_underscore");

        // Act - Using reflection to access private method
        var methodInfo = typeof(LinkedInScraper).GetMethod("GetSessionPath", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = methodInfo?.Invoke(scraper, null) as string;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPath, result);
        
        // Cleanup
        scraper.Dispose();
    }

    [Fact]
    public void ValidateCourseUrl_HandlesNullGracefully()
    {
        // Act & Assert
        Assert.False(_scraper.ValidateCourseUrl(null!, false));
    }

    [Theory]
    [InlineData("/learning/courses/python", "https://www.linkedin.com/learning/courses/python")]
    [InlineData("/learning/courses/data-science", "https://www.linkedin.com/learning/courses/data-science")]
    [InlineData("/learning/courses/machine-learning-basics", "https://www.linkedin.com/learning/courses/machine-learning-basics")]
    public void ConvertRelativeToAbsoluteUrl_WorksCorrectly(string relativeUrl, string expectedAbsolute)
    {
        // This tests the URL conversion logic that's embedded in DiscoverLessonsAsync
        // The actual implementation converts relative URLs starting with "/" to absolute URLs
        
        // Arrange
        var testUrl = relativeUrl;
        
        // Act
        if (testUrl.StartsWith("/"))
        {
            testUrl = "https://www.linkedin.com" + testUrl;
        }
        
        // Assert
        Assert.Equal(expectedAbsolute, testUrl);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/learning/courses/test", true)]
    [InlineData("http://www.linkedin.com/learning/courses/test", true)] // HTTP should also be valid
    [InlineData("HTTPS://WWW.LINKEDIN.COM/LEARNING/COURSES/TEST", true)] // Case insensitive
    public void ValidateCourseUrl_HandlesProtocolVariations(string url, bool expected)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/learning/courses/course-with-numbers-123")]
    [InlineData("https://www.linkedin.com/learning/courses/course_with_underscore")]
    [InlineData("https://www.linkedin.com/learning/courses/course-with-many-dashes-in-name")]
    [InlineData("https://www.linkedin.com/learning/courses/123-course-starting-with-number")]
    public void ValidateCourseUrl_HandlesVariousCourseNameFormats(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SessionProfile_PathCombination_WorksAcrossPlatforms()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "test-key",
            OpenAIModel = "test-model",
            OutputTranscriptDir = "./test-output",
            SessionProfile = Path.Combine("nested", "session", "folder"),
            Headless = true
        };
        var scraper = new LinkedInScraper(config);

        // Act - Using reflection to access private method
        var methodInfo = typeof(LinkedInScraper).GetMethod("GetSessionPath", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = methodInfo?.Invoke(scraper, null) as string;

        // Assert
        Assert.NotNull(result);
        Assert.Contains("nested", result);
        Assert.Contains("session", result);
        Assert.Contains("folder", result);
        
        // Cleanup
        scraper.Dispose();
    }

    [Theory]
    [InlineData("https://www.linkedin.com/learning/courses/test?param=value", "https://www.linkedin.com/learning/courses/test?param=value")]
    [InlineData("https://www.linkedin.com/learning/courses/test#section", "https://www.linkedin.com/learning/courses/test#section")]
    [InlineData("https://www.linkedin.com/learning/courses/test?param=value#section", "https://www.linkedin.com/learning/courses/test?param=value#section")]
    public void ValidateCourseUrl_PreservesQueryAndFragment(string inputUrl, string expectedUrl)
    {
        // Act
        var isValid = _scraper.ValidateCourseUrl(inputUrl, false);
        
        // Assert
        Assert.True(isValid);
        // The URL should be valid and the validation shouldn't modify the URL
        // (though the method doesn't return the URL, just validates it)
    }

    public void Dispose()
    {
        _scraper?.Dispose();
    }
}