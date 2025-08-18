using LinkedInLearningSummarizer.Models;
using Xunit;

namespace Tests;

public class AppConfigTests
{
    [Fact]
    public void Validate_WithValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "sk-valid-key",
            OpenAIModel = "gpt-4",
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 4000,
            MapChunkOverlap = 200
        };

        // Act & Assert
        // Should not throw any exception
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithMissingOpenAIApiKey_ThrowsException()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "", // Missing
            OpenAIModel = "gpt-4",
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 4000,
            MapChunkOverlap = 200
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("OPENAI_API_KEY is required", exception.Message);
    }

    [Fact]
    public void Validate_WithMissingOpenAIModel_ThrowsException()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "sk-valid-key",
            OpenAIModel = "", // Missing
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 4000,
            MapChunkOverlap = 200
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("OPENAI_MODEL is required", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidMaxScrollRounds_ThrowsException()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "sk-valid-key",
            OpenAIModel = "gpt-4",
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 0, // Invalid
            SinglePassThreshold = 5000,
            MapChunkSize = 4000,
            MapChunkOverlap = 200
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("MAX_SCROLL_ROUNDS must be greater than 0", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidSinglePassThreshold_ThrowsException()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "sk-valid-key",
            OpenAIModel = "gpt-4",
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 10,
            SinglePassThreshold = -1, // Invalid
            MapChunkSize = 4000,
            MapChunkOverlap = 200
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("SINGLE_PASS_THRESHOLD must be greater than 0", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidMapChunkSize_ThrowsException()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "sk-valid-key",
            OpenAIModel = "gpt-4",
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 0, // Invalid
            MapChunkOverlap = 200
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("MAP_CHUNK_SIZE must be greater than 0", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeMapChunkOverlap_ThrowsException()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "sk-valid-key",
            OpenAIModel = "gpt-4",
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 4000,
            MapChunkOverlap = -1 // Invalid
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("MAP_CHUNK_OVERLAP must be 0 or greater", exception.Message);
    }

    [Fact]
    public void Validate_WithMapChunkOverlapGreaterThanSize_ThrowsException()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "sk-valid-key",
            OpenAIModel = "gpt-4",
            OutputTranscriptDir = "./output",
            SessionProfile = "session",
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 1000,
            MapChunkOverlap = 1500 // Greater than chunk size
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("MAP_CHUNK_OVERLAP must be less than MAP_CHUNK_SIZE", exception.Message);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReportsAllErrors()
    {
        // Arrange
        var config = new AppConfig
        {
            OpenAIApiKey = "", // Missing
            OpenAIModel = "", // Missing
            OutputTranscriptDir = "", // Missing
            SessionProfile = "", // Missing
            MaxScrollRounds = 0, // Invalid
            SinglePassThreshold = -1, // Invalid
            MapChunkSize = 0, // Invalid
            MapChunkOverlap = -1 // Invalid
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("OPENAI_API_KEY is required", exception.Message);
        Assert.Contains("OPENAI_MODEL is required", exception.Message);
        Assert.Contains("OUTPUT_TRANSCRIPT_DIR is required", exception.Message);
        Assert.Contains("SESSION_PROFILE is required", exception.Message);
        Assert.Contains("MAX_SCROLL_ROUNDS must be greater than 0", exception.Message);
        Assert.Contains("SINGLE_PASS_THRESHOLD must be greater than 0", exception.Message);
        Assert.Contains("MAP_CHUNK_SIZE must be greater than 0", exception.Message);
        Assert.Contains("MAP_CHUNK_OVERLAP must be 0 or greater", exception.Message);
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        Assert.Empty(config.OpenAIApiKey);
        Assert.Equal("gpt-4o-mini", config.OpenAIModel);
        Assert.Equal("./prompts/summary.txt", config.SummaryInstructionPath);
        Assert.Equal("./output", config.OutputTranscriptDir);
        Assert.True(config.Headless);
        Assert.Equal("linkedin_session", config.SessionProfile);
        Assert.False(config.KeepTimestamps);
        Assert.Equal(10, config.MaxScrollRounds);
        Assert.Equal(5000, config.SinglePassThreshold);
        Assert.Equal(4000, config.MapChunkSize);
        Assert.Equal(200, config.MapChunkOverlap);
    }
}