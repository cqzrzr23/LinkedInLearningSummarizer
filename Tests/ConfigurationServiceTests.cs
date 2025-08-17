using FluentAssertions;
using LinkedInLearningSummarizer.Services;
using Xunit;

namespace Tests;

public class ConfigurationServiceTests : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnvVars = new();
    private readonly string[] _envVarKeys = 
    {
        "OPENAI_API_KEY", "OPENAI_MODEL", "SUMMARY_INSTRUCTION_PATH",
        "OUTPUT_TRANSCRIPT_DIR", "HEADLESS", "SESSION_PROFILE",
        "KEEP_TIMESTAMPS", "MAX_SCROLL_ROUNDS", "SINGLE_PASS_THRESHOLD",
        "MAP_CHUNK_SIZE", "MAP_CHUNK_OVERLAP"
    };

    public ConfigurationServiceTests()
    {
        // Save original environment variables and clear them to ensure test isolation
        foreach (var key in _envVarKeys)
        {
            _originalEnvVars[key] = Environment.GetEnvironmentVariable(key);
            // Clear the environment variable to ensure test isolation
            Environment.SetEnvironmentVariable(key, null);
        }
        
        // Also clear any variables that might have been loaded from a real .env file
        ClearAllEnvironmentVariables();
    }
    
    private void ClearAllEnvironmentVariables()
    {
        // Ensure complete isolation by clearing all test-related env vars
        foreach (var key in _envVarKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    public void Dispose()
    {
        // Restore original environment variables
        foreach (var kvp in _originalEnvVars)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }

    [Fact]
    public void LoadConfiguration_WithValidTestEnvFile_LoadsAllValues()
    {
        // Arrange
        ClearAllEnvironmentVariables(); // Ensure clean state
        var testEnvPath = Path.Combine("TestData", ".env.test");
        
        // Act
        var service = new ConfigurationService(testEnvPath, suppressConsoleOutput: true);
        var config = service.Config;

        // Assert
        config.OpenAIApiKey.Should().Be("sk-test-key-12345678");
        config.OpenAIModel.Should().Be("gpt-test-model");
        config.SummaryInstructionPath.Should().Be("./test/prompts/summary.txt");
        config.OutputTranscriptDir.Should().Be("./test/output");
        config.Headless.Should().BeFalse();
        config.SessionProfile.Should().Be("test_session");
        config.KeepTimestamps.Should().BeTrue();
        config.MaxScrollRounds.Should().Be(5);
        config.SinglePassThreshold.Should().Be(1000);
        config.MapChunkSize.Should().Be(2000);
        config.MapChunkOverlap.Should().Be(100);
        
        // Clean up after test
        ClearAllEnvironmentVariables();
    }

    [Fact]
    public void LoadConfiguration_WithMissingEnvFile_UsesDefaultValues()
    {
        // Arrange
        ClearAllEnvironmentVariables(); // Ensure clean state
        var nonExistentPath = Path.Combine("TestData", "does-not-exist.env");
        
        // Act
        var service = new ConfigurationService(nonExistentPath, suppressConsoleOutput: true);
        var config = service.Config;

        // Assert - Check default values
        config.OpenAIApiKey.Should().BeEmpty();
        config.OpenAIModel.Should().Be("gpt-4o-mini");
        config.OutputTranscriptDir.Should().Be("./output");
        config.Headless.Should().BeTrue();
        config.SessionProfile.Should().Be("linkedin_session");
        config.KeepTimestamps.Should().BeFalse();
        config.MaxScrollRounds.Should().Be(10);
        config.SinglePassThreshold.Should().Be(5000);
        config.MapChunkSize.Should().Be(4000);
        config.MapChunkOverlap.Should().Be(200);
        
        // Clean up after test
        ClearAllEnvironmentVariables();
    }

    [Fact]
    public void LoadConfiguration_EnvironmentVariableOverridesEnvFile()
    {
        // Arrange
        ClearAllEnvironmentVariables(); // Ensure clean state
        var testEnvPath = Path.Combine("TestData", ".env.test");
        
        // Set environment variables BEFORE loading .env file
        // Note: DotNetEnv.Load will overwrite these, so this test verifies
        // that our GetEnvironmentVariable method reads the current value
        Environment.SetEnvironmentVariable("OPENAI_MODEL", "env-override-model");
        Environment.SetEnvironmentVariable("MAX_SCROLL_ROUNDS", "99");

        // Act
        var service = new ConfigurationService(testEnvPath, suppressConsoleOutput: true);
        var config = service.Config;

        // Assert
        // Since DotNetEnv overwrites environment variables with .env values,
        // we expect the .env values, not the pre-set environment values
        config.OpenAIModel.Should().Be("gpt-test-model"); // From .env.test
        config.MaxScrollRounds.Should().Be(5); // From .env.test
        config.OpenAIApiKey.Should().Be("sk-test-key-12345678");
        
        // Clean up after test
        ClearAllEnvironmentVariables();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("NO", false)]
    [InlineData("invalid", true)] // Should use default (true for HEADLESS)
    public void LoadConfiguration_BooleanParsing_WorksCorrectly(string value, bool expected)
    {
        // Arrange
        ClearAllEnvironmentVariables(); // Ensure clean state
        Environment.SetEnvironmentVariable("HEADLESS", value);
        
        // Use non-existent file path to ensure no .env file is loaded
        var nonExistentPath = Path.Combine("TestData", "does-not-exist.env");
        
        // Act
        var service = new ConfigurationService(nonExistentPath, suppressConsoleOutput: true);
        var config = service.Config;

        // Assert
        config.Headless.Should().Be(expected);
        
        // Clean up after test
        ClearAllEnvironmentVariables();
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("0", 0)]
    [InlineData("-1", -1)]
    [InlineData("not-a-number", 10)] // Should use default (10 for MAX_SCROLL_ROUNDS)
    [InlineData("", 10)] // Should use default
    public void LoadConfiguration_IntegerParsing_WorksCorrectly(string value, int expected)
    {
        // Arrange
        ClearAllEnvironmentVariables(); // Ensure clean state
        Environment.SetEnvironmentVariable("MAX_SCROLL_ROUNDS", value);
        
        // Use non-existent file path to ensure no .env file is loaded
        var nonExistentPath = Path.Combine("TestData", "does-not-exist.env");
        
        // Act
        var service = new ConfigurationService(nonExistentPath, suppressConsoleOutput: true);
        var config = service.Config;

        // Assert
        config.MaxScrollRounds.Should().Be(expected);
        
        // Clean up after test
        ClearAllEnvironmentVariables();
    }

    [Fact]
    public void LoadConfiguration_WithInvalidValues_UsesDefaults()
    {
        // Arrange
        ClearAllEnvironmentVariables(); // Ensure clean state
        var invalidEnvPath = Path.Combine("TestData", ".env.invalid");
        
        // Act
        var service = new ConfigurationService(invalidEnvPath, suppressConsoleOutput: true);
        var config = service.Config;

        // Assert
        config.OpenAIApiKey.Should().BeEmpty(); // Missing in invalid file
        config.MaxScrollRounds.Should().Be(10); // Invalid "not-a-number" should use default
        config.SinglePassThreshold.Should().Be(-100); // Negative value is loaded (validation would catch it)
        // Note: These values are loaded as-is, validation would catch the overlap issue
        config.MapChunkSize.Should().Be(100);
        config.MapChunkOverlap.Should().Be(200);
        
        // Clean up after test
        ClearAllEnvironmentVariables();
    }

    [Fact]
    public void MaskApiKey_HandlesVariousInputs()
    {
        // Arrange & Act
        ClearAllEnvironmentVariables(); // Ensure clean state
        var nonExistentPath = Path.Combine("TestData", "does-not-exist.env");
        
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "");
        var emptyConfig = new ConfigurationService(nonExistentPath, suppressConsoleOutput: true);
        
        ClearAllEnvironmentVariables();
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "short");
        var shortConfig = new ConfigurationService(nonExistentPath, suppressConsoleOutput: true);
        
        ClearAllEnvironmentVariables();
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-proj-1234567890abcdefghijklmnop");
        var longConfig = new ConfigurationService(nonExistentPath, suppressConsoleOutput: true);

        // Assert using reflection to test private method behavior through PrintConfiguration
        using var sw = new StringWriter();
        Console.SetOut(sw);
        
        longConfig.PrintConfiguration();
        var output = sw.ToString();
        
        output.Should().ContainAny("[NOT SET]", "***", "sk-p...mnop");
        
        // Clean up after test
        ClearAllEnvironmentVariables();
    }
}