using FluentAssertions;
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
        config.Invoking(c => c.Validate()).Should().NotThrow();
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*OPENAI_API_KEY is required*");
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*OPENAI_MODEL is required*");
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*MAX_SCROLL_ROUNDS must be greater than 0*");
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*SINGLE_PASS_THRESHOLD must be greater than 0*");
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*MAP_CHUNK_SIZE must be greater than 0*");
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*MAP_CHUNK_OVERLAP must be 0 or greater*");
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*MAP_CHUNK_OVERLAP must be less than MAP_CHUNK_SIZE*");
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
        config.Invoking(c => c.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*OPENAI_API_KEY is required*")
            .And.Message.Should().Contain("OPENAI_MODEL is required")
            .And.Contain("OUTPUT_TRANSCRIPT_DIR is required")
            .And.Contain("SESSION_PROFILE is required")
            .And.Contain("MAX_SCROLL_ROUNDS must be greater than 0")
            .And.Contain("SINGLE_PASS_THRESHOLD must be greater than 0")
            .And.Contain("MAP_CHUNK_SIZE must be greater than 0")
            .And.Contain("MAP_CHUNK_OVERLAP must be 0 or greater");
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        config.OpenAIApiKey.Should().BeEmpty();
        config.OpenAIModel.Should().Be("gpt-4o-mini");
        config.SummaryInstructionPath.Should().Be("./prompts/summary.txt");
        config.OutputTranscriptDir.Should().Be("./output");
        config.Headless.Should().BeTrue();
        config.SessionProfile.Should().Be("linkedin_session");
        config.KeepTimestamps.Should().BeFalse();
        config.MaxScrollRounds.Should().Be(10);
        config.SinglePassThreshold.Should().Be(5000);
        config.MapChunkSize.Should().Be(4000);
        config.MapChunkOverlap.Should().Be(200);
    }
}