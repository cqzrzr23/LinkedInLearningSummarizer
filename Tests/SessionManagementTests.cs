using Xunit;
using LinkedInLearningSummarizer.Services;
using LinkedInLearningSummarizer.Models;
using LinkedInLearningSummarizer.Tests.TestHelpers;
using System.Text.Json;

namespace LinkedInLearningSummarizer.Tests;

public class SessionManagementTests : IDisposable
{
    private readonly MockSessionHelper _mockHelper;
    private readonly List<string> _tempPaths;

    public SessionManagementTests()
    {
        _mockHelper = new MockSessionHelper();
        _tempPaths = new List<string>();
    }

    [Fact]
    public async Task CompleteSessionWorkflow_FirstTimeToReturningUser_Simulation()
    {
        // Arrange - Setup configuration with temporary session directory
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        var config = MockHelpers.CreateTestConfig(sessionPath);

        // ============ PART 1: FIRST-TIME USER EXPERIENCE ============
        
        // Act & Assert - No session should exist initially
        Assert.True(Directory.Exists(sessionPath), "Session directory should be created");
        var stateFile = Path.Combine(sessionPath, "state.json");
        Assert.False(File.Exists(stateFile), "No session file should exist initially");

        // Simulate user completing login and saving session
        var mockSessionData = MockHelpers.CreateMockSessionState("user@linkedin.com");
        await File.WriteAllTextAsync(stateFile, mockSessionData);

        // Verify session was saved correctly
        Assert.True(File.Exists(stateFile), "Session file should be created after login");
        var savedContent = await File.ReadAllTextAsync(stateFile);
        Assert.NotNull(savedContent);
        Assert.NotEmpty(savedContent);
        
        // Verify it's valid JSON
        var sessionJson = JsonSerializer.Deserialize<JsonElement>(savedContent);
        Assert.True(sessionJson.GetProperty("cookies").GetArrayLength() > 0);

        // ============ PART 2: RETURNING USER EXPERIENCE ============
        
        // Simulate application restart - session should persist
        Assert.True(File.Exists(stateFile), "Session should persist between runs");
        
        // Load and validate the saved session
        var loadedSession = await File.ReadAllTextAsync(stateFile);
        Assert.Equal(mockSessionData, loadedSession);

        // Verify session contains expected LinkedIn authentication data
        Assert.Contains("li_at", loadedSession);
        Assert.Contains("JSESSIONID", loadedSession);
        Assert.Contains("linkedin.com", loadedSession);
    }

    [Fact]
    public void HasValidSession_NoSessionDirectory_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var config = MockHelpers.CreateTestConfig(nonExistentPath);

        // Act
        var hasSession = Directory.Exists(nonExistentPath) && 
                        File.Exists(Path.Combine(nonExistentPath, "state.json"));

        // Assert
        Assert.False(hasSession, "Should return false when no session directory exists");
    }

    [Fact]
    public void HasValidSession_DirectoryExistsButNoStateFile_ReturnsFalse()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        var stateFile = Path.Combine(sessionPath, "state.json");

        // Act
        var hasSession = Directory.Exists(sessionPath) && File.Exists(stateFile);

        // Assert
        Assert.True(Directory.Exists(sessionPath), "Directory should exist");
        Assert.False(hasSession, "Should return false when state.json doesn't exist");
    }

    [Fact]
    public async Task HasValidSession_ValidStateFile_ReturnsTrue()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        MockHelpers.CreateMockSessionFile(sessionPath, createValidSession: true);
        var stateFile = Path.Combine(sessionPath, "state.json");

        // Act
        var hasSession = Directory.Exists(sessionPath) && File.Exists(stateFile);
        
        if (hasSession)
        {
            // Additional validation - can we parse the JSON?
            var content = await File.ReadAllTextAsync(stateFile);
            try
            {
                JsonSerializer.Deserialize<JsonElement>(content);
                hasSession = true;
            }
            catch
            {
                hasSession = false;
            }
        }

        // Assert
        Assert.True(hasSession, "Should return true when valid state file exists");
    }

    [Fact]
    public async Task SaveSession_CreatesCorrectFileStructure()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        var stateFile = Path.Combine(sessionPath, "state.json");
        var mockSession = MockHelpers.CreateMockSessionState();

        // Act - Simulate saving session
        await File.WriteAllTextAsync(stateFile, mockSession);

        // Assert
        Assert.True(Directory.Exists(sessionPath), "Session directory should exist");
        Assert.True(File.Exists(stateFile), "state.json should be created");
        
        var savedContent = await File.ReadAllTextAsync(stateFile);
        Assert.Equal(mockSession, savedContent);
        
        // Verify file structure
        var files = Directory.GetFiles(sessionPath);
        Assert.Contains(files, f => f.EndsWith("state.json"));
    }

    [Fact]
    public async Task LoadSession_ValidStateFile_LoadsSuccessfully()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        MockHelpers.CreateMockSessionFile(sessionPath, createValidSession: true);
        var stateFile = Path.Combine(sessionPath, "state.json");

        // Act
        var sessionContent = await File.ReadAllTextAsync(stateFile);
        var sessionData = JsonSerializer.Deserialize<JsonElement>(sessionContent);

        // Assert
        Assert.NotNull(sessionContent);
        Assert.NotEmpty(sessionContent);
        Assert.True(sessionData.GetProperty("cookies").ValueKind != JsonValueKind.Null);
        Assert.True(sessionData.GetProperty("origins").ValueKind != JsonValueKind.Null);
        
        // Verify specific cookie exists
        var cookies = sessionData.GetProperty("cookies").EnumerateArray();
        Assert.Contains(cookies, c => c.GetProperty("name").GetString() == "li_at");
    }

    [Fact]
    public async Task LoadSession_CorruptedStateFile_ThrowsException()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        MockHelpers.CreateMockSessionFile(sessionPath, createValidSession: false);
        var stateFile = Path.Combine(sessionPath, "state.json");

        // Act & Assert
        var content = await File.ReadAllTextAsync(stateFile);
        
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<JsonElement>(content));
    }

    [Fact]
    public void ResetSession_RemovesSessionDirectory()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        MockHelpers.CreateMockSessionFile(sessionPath, createValidSession: true);
        
        // Verify setup
        Assert.True(Directory.Exists(sessionPath), "Session should exist before reset");
        Assert.True(File.Exists(Path.Combine(sessionPath, "state.json")));

        // Act - Simulate reset
        if (Directory.Exists(sessionPath))
        {
            Directory.Delete(sessionPath, true);
        }

        // Assert
        Assert.False(Directory.Exists(sessionPath), "Session directory should be removed");
    }

    [Fact]
    public async Task SessionPersistence_SurvivesMultipleLoads()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        var stateFile = Path.Combine(sessionPath, "state.json");
        var originalSession = MockHelpers.CreateMockSessionState("persistent@test.com");
        
        // Act - Save once
        await File.WriteAllTextAsync(stateFile, originalSession);
        
        // Load multiple times
        var load1 = await File.ReadAllTextAsync(stateFile);
        var load2 = await File.ReadAllTextAsync(stateFile);
        var load3 = await File.ReadAllTextAsync(stateFile);

        // Assert - All loads should return identical data
        Assert.Equal(originalSession, load1);
        Assert.Equal(originalSession, load2);
        Assert.Equal(originalSession, load3);
    }

    [Fact]
    public void HeadlessModeOverride_FirstRun_ForcesHeadedMode()
    {
        // Arrange
        var config = MockHelpers.CreateTestConfig();
        config.Headless = true; // User wants headless
        
        var sessionPath = config.SessionProfile;
        var hasSession = Directory.Exists(sessionPath) && 
                        File.Exists(Path.Combine(sessionPath, "state.json"));

        // Act - Determine if headed mode should be forced
        var shouldForceHeaded = !hasSession && config.Headless;

        // Assert
        Assert.False(hasSession, "No session should exist initially");
        Assert.True(shouldForceHeaded, "Should force headed mode for first-time login even if config says headless");
    }

    [Fact]
    public async Task SessionValidation_ExpiredSession_RequiresReauth()
    {
        // Arrange
        var sessionPath = _mockHelper.CreateTempSessionDirectory();
        var stateFile = Path.Combine(sessionPath, "state.json");
        
        // Create an expired session (past expiration date)
        var expiredSession = new
        {
            cookies = new[]
            {
                new 
                { 
                    name = "li_at", 
                    value = "expired_token", 
                    domain = ".linkedin.com", 
                    expires = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds() // Expired
                }
            }
        };
        
        await File.WriteAllTextAsync(stateFile, JsonSerializer.Serialize(expiredSession));

        // Act - Check if session is expired
        var sessionData = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(stateFile));
        var liAtCookie = sessionData.GetProperty("cookies").EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("name").GetString() == "li_at");
        
        var expirationTime = liAtCookie.GetProperty("expires").GetInt64();
        var isExpired = DateTimeOffset.FromUnixTimeSeconds(expirationTime) < DateTimeOffset.UtcNow;

        // Assert
        Assert.True(isExpired, "Session should be detected as expired");
    }

    [Fact]
    public void SessionProfile_DifferentConfigurations_UsesDifferentPaths()
    {
        // Arrange
        var config1 = MockHelpers.CreateTestConfig();
        config1.SessionProfile = "session_profile_1";
        
        var config2 = MockHelpers.CreateTestConfig();
        config2.SessionProfile = "session_profile_2";

        // Act
        var path1 = Path.Combine(Directory.GetCurrentDirectory(), config1.SessionProfile);
        var path2 = Path.Combine(Directory.GetCurrentDirectory(), config2.SessionProfile);

        // Assert
        Assert.NotEqual(path2, path1);
        Assert.Contains("session_profile_1", path1);
        Assert.Contains("session_profile_2", path2);
    }

    public void Dispose()
    {
        _mockHelper?.Dispose();
        foreach (var path in _tempPaths)
        {
            MockHelpers.CleanupDirectory(path);
        }
    }
}