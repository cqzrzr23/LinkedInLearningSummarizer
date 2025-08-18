using Xunit;

namespace LinkedInLearningSummarizer.Tests;

public class URLParsingTests
{
    [Fact]
    public void ParseUrls_IgnoresCommentsAndEmptyLines()
    {
        // Arrange
        var lines = new[]
        {
            "# Comment line",
            "https://www.linkedin.com/learning/course-1",
            "",
            "  # Indented comment",
            "https://www.linkedin.com/learning/course-2",
            "   ",
            "# Final comment"
        };

        // Act
        var validUrls = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        // Assert
        Assert.Equal(2, validUrls.Count);
        Assert.Contains("https://www.linkedin.com/learning/course-1", validUrls);
        Assert.Contains("https://www.linkedin.com/learning/course-2", validUrls);
        Assert.DoesNotContain(validUrls, line => line.StartsWith("#"));
    }

    [Fact]
    public void ParseUrls_HandlesEmptyFile()
    {
        // Arrange
        var lines = Array.Empty<string>();

        // Act
        var validUrls = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        // Assert
        Assert.Empty(validUrls);
    }

    [Fact]
    public void ParseUrls_HandlesCommentOnlyFile()
    {
        // Arrange
        var lines = new[]
        {
            "# This is a comment file",
            "# Another comment",
            "  # Indented comment",
            "# No actual URLs here"
        };

        // Act
        var validUrls = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        // Assert
        Assert.Empty(validUrls);
    }

    [Fact]
    public void ParseUrls_PreservesUrlOrder()
    {
        // Arrange
        var lines = new[]
        {
            "https://www.linkedin.com/learning/first",
            "https://www.linkedin.com/learning/second",
            "https://www.linkedin.com/learning/third"
        };

        // Act
        var validUrls = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        // Assert
        Assert.Equal(3, validUrls.Count);
        Assert.Equal("https://www.linkedin.com/learning/first", validUrls[0]);
        Assert.Equal("https://www.linkedin.com/learning/second", validUrls[1]);
        Assert.Equal("https://www.linkedin.com/learning/third", validUrls[2]);
    }

    [Fact]
    public void ParseUrls_TrimsWhitespace()
    {
        // Arrange
        var lines = new[]
        {
            "  https://www.linkedin.com/learning/course-with-spaces  ",
            "\thttps://www.linkedin.com/learning/course-with-tabs\t",
            " \t https://www.linkedin.com/learning/mixed-whitespace \t "
        };

        // Act
        var validUrls = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        // Assert
        Assert.Equal(3, validUrls.Count);
        Assert.DoesNotContain(validUrls, url => url.StartsWith(" ") || url.EndsWith(" "));
        Assert.DoesNotContain(validUrls, url => url.StartsWith("\t") || url.EndsWith("\t"));
    }

    [Fact]
    public void ParseUrls_HandlesWindowsAndUnixLineEndings()
    {
        // Arrange
        var contentWindows = "url1\r\nurl2\r\nurl3";
        var contentUnix = "url1\nurl2\nurl3";

        // Act
        var urlsWindows = contentWindows.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var urlsUnix = contentUnix.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Assert
        Assert.Equal(3, urlsWindows.Length);
        Assert.Equal(3, urlsUnix.Length);
        Assert.Equal(urlsUnix, urlsWindows);
    }

    [Fact]
    public void ParseUrls_AcceptsVariousLinkedInLearningFormats()
    {
        // Arrange
        var lines = new[]
        {
            "https://www.linkedin.com/learning/course-name",
            "https://www.linkedin.com/learning/course-name/lesson-name",
            "https://www.linkedin.com/learning/course-name?u=123456",
            "https://www.linkedin.com/learning/paths/path-name",
            "http://www.linkedin.com/learning/course-name" // HTTP variant
        };

        // Act
        var validUrls = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        // Assert
        Assert.Equal(5, validUrls.Count);
        Assert.All(validUrls, url => Assert.Contains("linkedin.com/learning", url));
    }

    [Fact]
    public void ParseUrls_HandlesLargeFiles()
    {
        // Arrange - Simulate a large file with 1000 URLs
        var lines = new List<string> { "# Large batch of courses" };
        for (int i = 1; i <= 1000; i++)
        {
            lines.Add($"https://www.linkedin.com/learning/course-{i}");
            if (i % 100 == 0)
                lines.Add($"# Milestone {i}");
        }

        // Act
        var validUrls = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        // Assert
        Assert.Equal(1000, validUrls.Count);
        Assert.Equal("https://www.linkedin.com/learning/course-1", validUrls.First());
        Assert.Equal("https://www.linkedin.com/learning/course-1000", validUrls.Last());
    }

    [Fact]
    public void ValidateLinkedInUrl_RejectsInvalidUrls()
    {
        // Arrange
        var invalidUrls = new[]
        {
            "not-a-url",
            "https://www.google.com",
            "https://www.udemy.com/course",
            "ftp://linkedin.com/learning/course",
            "linkedin.com/learning/course", // Missing protocol
            "https://linkedin.com", // Missing /learning path
            ""
        };

        // Act & Assert
        foreach (var url in invalidUrls)
        {
            var isValid = url.StartsWith("http") && url.Contains("linkedin.com/learning");
            Assert.False(isValid, $"{url} should not be considered a valid LinkedIn Learning URL");
        }
    }

    [Fact]
    public void ValidateLinkedInUrl_AcceptsValidUrls()
    {
        // Arrange
        var validUrls = new[]
        {
            "https://www.linkedin.com/learning/course-name",
            "http://www.linkedin.com/learning/course-name",
            "https://www.linkedin.com/learning/paths/become-a-developer",
            "https://www.linkedin.com/learning/course?param=value"
        };

        // Act & Assert
        foreach (var url in validUrls)
        {
            var isValid = url.StartsWith("http") && url.Contains("linkedin.com/learning");
            Assert.True(isValid, $"{url} should be considered a valid LinkedIn Learning URL");
        }
    }
}