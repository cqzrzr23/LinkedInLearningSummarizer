using LinkedInLearningSummarizer.Models;
using LinkedInLearningSummarizer.Services;
using Xunit;

namespace Tests;

public class CourseUrlValidationTests
{
    private readonly AppConfig _testConfig;
    private readonly LinkedInScraper _scraper;

    public CourseUrlValidationTests()
    {
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

    [Theory]
    [InlineData("https://www.linkedin.com/learning/courses/python-essential-training")]
    [InlineData("https://www.linkedin.com/learning/courses/python-essential-training/")]
    [InlineData("https://www.linkedin.com/learning/courses/python-essential-training?u=12345")]
    [InlineData("https://www.linkedin.com/learning/courses/python-essential-training-18764650")]
    [InlineData("https://linkedin.com/learning/courses/data-science-foundations")]
    [InlineData("https://www.linkedin.com/learning/courses/c-sharp-design-patterns-part-1")]
    public void ValidateCourseUrl_ValidLinkedInLearningUrls_ReturnsTrue(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("https://www.google.com/learning/courses/test")]
    [InlineData("https://www.udemy.com/course/python")]
    [InlineData("https://www.coursera.org/learn/python")]
    [InlineData("https://www.youtube.com/watch?v=123")]
    [InlineData("http://example.com/courses/test")]
    public void ValidateCourseUrl_NonLinkedInDomains_ReturnsFalse(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/in/john-doe")]
    [InlineData("https://www.linkedin.com/posts/something")]
    [InlineData("https://www.linkedin.com/feed/")]
    [InlineData("https://www.linkedin.com/jobs/")]
    [InlineData("https://www.linkedin.com/company/microsoft")]
    public void ValidateCourseUrl_LinkedInButNotLearning_ReturnsFalse(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/learning/")]
    [InlineData("https://www.linkedin.com/learning/browse")]
    [InlineData("https://www.linkedin.com/learning/topics/python")]
    [InlineData("https://www.linkedin.com/learning/paths/become-a-developer")]
    [InlineData("https://www.linkedin.com/learning/subscription")]
    public void ValidateCourseUrl_LearningButNotCourse_ReturnsFalse(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void ValidateCourseUrl_EmptyOrWhitespace_ReturnsFalse(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCourseUrl_NullUrl_ReturnsFalse()
    {
        // Act
        var result = _scraper.ValidateCourseUrl(null!, false);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://linkedin.com/learning/courses/test")]
    [InlineData("linkedin.com/learning/courses/test")]
    [InlineData("just some random text")]
    [InlineData("http://")]
    [InlineData("https://")]
    public void ValidateCourseUrl_MalformedUrls_ReturnsFalse(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("HTTPS://WWW.LINKEDIN.COM/LEARNING/COURSES/PYTHON")]
    [InlineData("HtTpS://WwW.LiNkEdIn.CoM/LeArNiNg/CoUrSeS/python")]
    public void ValidateCourseUrl_DifferentCasing_ReturnsTrue(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/learning/courses/python#chapter1")]
    [InlineData("https://www.linkedin.com/learning/courses/python?autoplay=true&resume=false")]
    [InlineData("https://www.linkedin.com/learning/courses/python?u=12345&success=true&trk=test")]
    public void ValidateCourseUrl_WithFragmentsAndQueryParams_ReturnsTrue(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("https://de.linkedin.com/learning/courses/python")]
    [InlineData("https://fr.linkedin.com/learning/courses/python")]
    [InlineData("https://uk.linkedin.com/learning/courses/python")]
    [InlineData("https://es.linkedin.com/learning/courses/python")]
    public void ValidateCourseUrl_InternationalDomains_ReturnsTrue(string url)
    {
        // Act
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("//www.linkedin.com/learning/courses/test")]
    [InlineData("//linkedin.com/learning/courses/python")]
    public void ValidateCourseUrl_ProtocolRelativeUrls_ReturnsTrue(string url)
    {
        // Act - Protocol-relative URLs (starting with //) should be handled by prepending https:
        var result = _scraper.ValidateCourseUrl(url, false);

        // Assert
        Assert.True(result);
    }

    public void Dispose()
    {
        _scraper?.Dispose();
    }
}