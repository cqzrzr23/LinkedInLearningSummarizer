using System.Text.Json;
using System.Diagnostics;
using LinkedInLearningSummarizer.Models;

namespace LinkedInLearningSummarizer.Tests.TestHelpers;

public static class MockHelpers
{
    public static string CreateMockSessionState(string userEmail = "test@example.com")
    {
        var sessionData = new
        {
            cookies = new object[]
            {
                new 
                { 
                    name = "li_at", 
                    value = "AQEDABcNZ8wEFoU7AAABjR_mock_session_token", 
                    domain = ".linkedin.com", 
                    path = "/", 
                    expires = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
                    httpOnly = true,
                    secure = true,
                    sameSite = "None"
                },
                new 
                { 
                    name = "JSESSIONID", 
                    value = "ajax:0123456789012345678", 
                    domain = ".linkedin.com",
                    path = "/",
                    httpOnly = true,
                    secure = true,
                    sameSite = (string?)null,
                    expires = (long?)null
                },
                new 
                { 
                    name = "lang", 
                    value = "v=2&lang=en-us", 
                    domain = ".linkedin.com",
                    path = "/",
                    httpOnly = (bool?)null,
                    secure = (bool?)null,
                    sameSite = (string?)null,
                    expires = (long?)null
                },
                new
                {
                    name = "lidc",
                    value = "\"b=VGST04:s=V:r=V:a=V:p=V:g=2950:u=1:x=1:i=1234567890:t=1234567890:v=2:sig=AQFJ_mock\"",
                    domain = ".linkedin.com",
                    path = "/",
                    httpOnly = (bool?)null,
                    secure = (bool?)null,
                    sameSite = (string?)null,
                    expires = (long?)null
                }
            },
            origins = new[]
            {
                new
                {
                    origin = "https://www.linkedin.com",
                    localStorage = new[]
                    {
                        new { name = "userAccount", value = userEmail },
                        new { name = "sessionStart", value = DateTime.UtcNow.ToString("O") },
                        new { name = "learning_history", value = "[]" }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(sessionData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    public static string GetTemporaryDirectory(string prefix = "test_session")
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    public static void CleanupDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    public static AppConfig CreateTestConfig(string? sessionPath = null)
    {
        return new AppConfig
        {
            OpenAIApiKey = "test-api-key",
            OpenAIModel = "gpt-4o-mini",
            OutputTranscriptDir = Path.Combine(Path.GetTempPath(), "test_output"),
            SessionProfile = sessionPath ?? GetTemporaryDirectory(),
            Headless = true,
            KeepTimestamps = false,
            MaxScrollRounds = 10,
            SinglePassThreshold = 5000,
            MapChunkSize = 4000,
            MapChunkOverlap = 200,
            SummaryInstructionPath = "./prompts/summary.txt"
        };
    }

    public static void CreateMockSessionFile(string sessionPath, bool createValidSession = true)
    {
        Directory.CreateDirectory(sessionPath);
        var stateFile = Path.Combine(sessionPath, "state.json");
        
        if (createValidSession)
        {
            File.WriteAllText(stateFile, CreateMockSessionState());
        }
        else
        {
            // Create corrupted session for testing error handling
            File.WriteAllText(stateFile, "{ invalid json data");
        }
    }

    public static string CreateTestUrlsFile(params string[] urls)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, urls);
        return tempFile;
    }

    public static async Task<(int exitCode, string output, string error)> RunProgramAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{GetProjectPath()}\" -- {string.Join(" ", args)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = GetProjectDirectory()
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }

    private static string GetProjectPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            var projectFile = Path.Combine(currentDir, "LinkedInLearningSummarizer.csproj");
            if (File.Exists(projectFile))
                return projectFile;
            
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        
        // Fallback to known path
        return @"E:\Projects\LinkedInLearningSummarizer\LinkedInLearningSummarizer.csproj";
    }

    private static string GetProjectDirectory()
    {
        return Path.GetDirectoryName(GetProjectPath()) ?? 
               @"E:\Projects\LinkedInLearningSummarizer";
    }

    public static void SetEnvironmentVariables(Dictionary<string, string> variables)
    {
        foreach (var kvp in variables)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }

    public static void ClearEnvironmentVariables(params string[] keys)
    {
        foreach (var key in keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}

public class MockSessionHelper : IDisposable
{
    private readonly List<string> _tempDirectories = new();
    private readonly List<string> _tempFiles = new();

    public string CreateTempSessionDirectory()
    {
        var path = MockHelpers.GetTemporaryDirectory();
        _tempDirectories.Add(path);
        return path;
    }

    public string CreateTempUrlsFile(params string[] urls)
    {
        var path = MockHelpers.CreateTestUrlsFile(urls);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            MockHelpers.CleanupDirectory(dir);
        }

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