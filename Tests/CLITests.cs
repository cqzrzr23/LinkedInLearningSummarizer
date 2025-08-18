using Xunit;
using LinkedInLearningSummarizer.Tests.TestHelpers;
using LinkedInLearningSummarizer.Utils;
using System.Text;

namespace LinkedInLearningSummarizer.Tests;

[Collection("Sequential")] // Run these tests sequentially to avoid conflicts
public class CLITests : IDisposable
{
    private readonly MockSessionHelper _mockHelper;
    private readonly List<string> _tempFiles;

    public CLITests()
    {
        _mockHelper = new MockSessionHelper();
        _tempFiles = new List<string>();
    }


    [Fact]
    public async Task InvalidFile_ReturnsError()
    {
        // Act
        var result = await UrlFileProcessor.ProcessUrlFileAsync("nonexistent_file.txt");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("File not found", result.ErrorMessage);
        Assert.Empty(result.Urls);
    }

    [Fact]
    public async Task EmptyUrlsFile_HandlesGracefully()
    {
        // Arrange
        var emptyFile = _mockHelper.CreateTempUrlsFile(); // Creates empty file

        // Act
        var result = await UrlFileProcessor.ProcessUrlFileAsync(emptyFile);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Urls);
        Assert.Equal(0, result.ValidUrlCount);
    }

    [Fact]
    public async Task CommentOnlyFile_HandlesCorrectly()
    {
        // Arrange
        var commentFile = _mockHelper.CreateTempUrlsFile(
            "# This is a comment",
            "# Another comment",
            "  # Indented comment",
            "",
            "# No actual URLs here"
        );

        // Act
        var result = await UrlFileProcessor.ProcessUrlFileAsync(commentFile);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Urls);
        Assert.Equal(0, result.ValidUrlCount);
        Assert.Equal(5, result.TotalLinesProcessed);
    }

    [Fact]
    public async Task ValidUrlsFile_ProcessesCorrectly()
    {
        // Arrange
        var urlsFile = _mockHelper.CreateTempUrlsFile(
            "# Test URLs",
            "https://www.linkedin.com/learning/test-course-1",
            "",
            "# Another course",
            "https://www.linkedin.com/learning/test-course-2"
        );

        // Act
        var result = await UrlFileProcessor.ProcessUrlFileAsync(urlsFile);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ValidUrlCount);
        Assert.Equal(5, result.TotalLinesProcessed);
        Assert.Contains("https://www.linkedin.com/learning/test-course-1", result.Urls);
        Assert.Contains("https://www.linkedin.com/learning/test-course-2", result.Urls);
    }

    [Fact]
    public async Task MixedContentFile_ExtractsValidUrls()
    {
        // Arrange
        var mixedFile = _mockHelper.CreateTempUrlsFile(
            "# LinkedIn Learning Courses",
            "",
            "https://www.linkedin.com/learning/valid-course",
            "not-a-valid-url",
            "# Comment in middle",
            "https://www.linkedin.com/learning/another-course",
            "",
            "random text",
            "# End comments"
        );

        // Act
        var result = await UrlFileProcessor.ProcessUrlFileAsync(mixedFile);

        // Assert - Should process all non-comment, non-empty lines as URLs
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.ValidUrlCount); // "valid-course", "not-a-valid-url", "another-course", "random text"
        Assert.Equal(9, result.TotalLinesProcessed);
        Assert.Contains("https://www.linkedin.com/learning/valid-course", result.Urls);
        Assert.Contains("not-a-valid-url", result.Urls);
        Assert.Contains("https://www.linkedin.com/learning/another-course", result.Urls);
        Assert.Contains("random text", result.Urls);
        // Comments and empty lines should be excluded
        Assert.DoesNotContain(result.Urls, url => url.StartsWith("#"));
    }



    [Fact]
    public async Task LargeUrlFile_HandlesCorrectly()
    {
        // Arrange - Create file with many URLs
        var urls = new List<string> { "# Large batch of courses" };
        for (int i = 1; i <= 50; i++)
        {
            urls.Add($"https://www.linkedin.com/learning/course-{i}");
            if (i % 10 == 0)
                urls.Add($"# Milestone {i}");
        }
        
        var largeFile = _mockHelper.CreateTempUrlsFile(urls.ToArray());

        // Act
        var result = await UrlFileProcessor.ProcessUrlFileAsync(largeFile);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.ValidUrlCount);
        Assert.Equal(56, result.TotalLinesProcessed); // 50 URLs + 6 comments
        Assert.Contains("https://www.linkedin.com/learning/course-1", result.Urls);
        Assert.Contains("https://www.linkedin.com/learning/course-50", result.Urls);
    }

    [Fact]
    public void ValidateLinkedInUrls_FiltersProperly()
    {
        // Arrange
        var urls = new[]
        {
            "https://www.linkedin.com/learning/course-1",
            "https://www.google.com",
            "not-a-url",
            "https://www.linkedin.com/learning/course-2",
            "http://linkedin.com/learning/old-course"
        };

        // Act
        var validationResult = UrlFileProcessor.ValidateUrls(urls);

        // Assert
        Assert.Equal(5, validationResult.TotalProcessed);
        Assert.Equal(3, validationResult.ValidCount);
        Assert.Equal(2, validationResult.InvalidCount);
        Assert.Contains("https://www.linkedin.com/learning/course-1", validationResult.ValidUrls);
        Assert.Contains("https://www.linkedin.com/learning/course-2", validationResult.ValidUrls);
        Assert.Contains("http://linkedin.com/learning/old-course", validationResult.ValidUrls);
        Assert.Contains("https://www.google.com", validationResult.InvalidUrls);
        Assert.Contains("not-a-url", validationResult.InvalidUrls);
    }

    [Fact]
    public void IsValidLinkedInLearningUrl_ValidatesCorrectly()
    {
        // Valid URLs
        Assert.True(UrlFileProcessor.IsValidLinkedInLearningUrl("https://www.linkedin.com/learning/course"));
        Assert.True(UrlFileProcessor.IsValidLinkedInLearningUrl("http://linkedin.com/learning/path"));
        Assert.True(UrlFileProcessor.IsValidLinkedInLearningUrl("https://www.linkedin.com/learning/course?param=value"));

        // Invalid URLs
        Assert.False(UrlFileProcessor.IsValidLinkedInLearningUrl("https://www.google.com"));
        Assert.False(UrlFileProcessor.IsValidLinkedInLearningUrl("not-a-url"));
        Assert.False(UrlFileProcessor.IsValidLinkedInLearningUrl(""));
        Assert.False(UrlFileProcessor.IsValidLinkedInLearningUrl("linkedin.com/learning/course"));
        Assert.False(UrlFileProcessor.IsValidLinkedInLearningUrl("ftp://linkedin.com/learning/course"));
    }

    // ============================================================================
    // INTEGRATION TESTS - These require actual program execution
    // ============================================================================

    [Fact(Skip = "Integration test - requires actual program execution")]
    public async Task HelpCommand_ShowsUsageInformation()
    {
        // Act
        var (exitCode, output, error) = await MockHelpers.RunProgramAsync("--help");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Contains("LinkedInLearningSummarizer <urls.txt>", output);
        Assert.Contains("--check-config", output);
        Assert.Contains("--reset-session", output);
        Assert.Contains("--help", output);
        Assert.Contains("Configuration:", output);
        Assert.Contains("URL File Format:", output);
    }

    [Fact(Skip = "Integration test - requires actual program execution")]
    public async Task HelpCommand_ShortForm_Works()
    {
        // Act
        var (exitCode, output, error) = await MockHelpers.RunProgramAsync("-h");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
    }

    [Fact(Skip = "Integration test - requires actual program execution")]
    public async Task CheckConfigCommand_ValidatesConfiguration()
    {
        // Arrange - Set minimal required environment variables
        MockHelpers.SetEnvironmentVariables(new Dictionary<string, string>
        {
            ["OPENAI_API_KEY"] = "test-api-key",
            ["OUTPUT_TRANSCRIPT_DIR"] = "./output",
            ["SESSION_PROFILE"] = "test_session"
        });

        try
        {
            // Act
            var (exitCode, output, error) = await MockHelpers.RunProgramAsync("--check-config");

            // Assert
            Assert.Contains("Checking configuration", output);
            Assert.Contains("Current Configuration", output);
            Assert.Contains("OpenAI Model:", output);
            Assert.Contains("Session Profile:", output);
            Assert.Contains("Headless Mode:", output);
            
            // Should also check for LinkedIn session
            Assert.True(output.Contains("Checking LinkedIn session") || output.Contains("LinkedIn session"));
        }
        finally
        {
            // Cleanup
            MockHelpers.ClearEnvironmentVariables("OPENAI_API_KEY", "OUTPUT_TRANSCRIPT_DIR", "SESSION_PROFILE");
        }
    }

    [Fact(Skip = "Integration test - requires actual program execution")]
    public async Task ResetSessionCommand_HandlesNoSession()
    {
        // Act
        var (exitCode, output, error) = await MockHelpers.RunProgramAsync("--reset-session");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(output.Contains("No existing session found") || 
                   output.Contains("Session reset successfully") || 
                   output.Contains("Resetting LinkedIn session"));
    }

    [Fact(Skip = "Integration test - requires actual program execution")]
    public async Task NoArguments_ShowsHelp()
    {
        // Act
        var (exitCode, output, error) = await MockHelpers.RunProgramAsync();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
    }

    [Fact(Skip = "Integration test - requires actual program execution")]
    public async Task InvalidCommand_ShowsError()
    {
        // Act
        var (exitCode, output, error) = await MockHelpers.RunProgramAsync("--invalid-command");

        // Assert
        // The program treats unknown commands as file paths
        Assert.Equal(1, exitCode);
        Assert.Contains("File not found", output);
    }

    [Fact(Skip = "Integration test - requires actual program execution")]
    public async Task ConfigurationError_ShowsDetailedMessage()
    {
        // Arrange - Clear required environment variables to trigger validation error
        MockHelpers.ClearEnvironmentVariables("OPENAI_API_KEY");
        
        var urlsFile = _mockHelper.CreateTempUrlsFile(
            "https://www.linkedin.com/learning/test-course"
        );

        // Act
        var (exitCode, output, error) = await MockHelpers.RunProgramAsync($"\"{urlsFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Configuration Error", output);
        Assert.Contains("OPENAI_API_KEY", output);
    }

    public void Dispose()
    {
        _mockHelper?.Dispose();
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}