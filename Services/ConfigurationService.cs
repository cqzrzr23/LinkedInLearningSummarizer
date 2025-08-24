using LinkedInLearningSummarizer.Models;

namespace LinkedInLearningSummarizer.Services;

public class ConfigurationService
{
    private readonly AppConfig _config;
    private readonly bool _suppressConsoleOutput;

    public ConfigurationService(string? envFilePath = null, bool suppressConsoleOutput = false)
    {
        _suppressConsoleOutput = suppressConsoleOutput;
        _config = LoadConfiguration(envFilePath);
    }

    public AppConfig Config => _config;

    private AppConfig LoadConfiguration(string? envFilePath)
    {
        // Use provided path or default to .env in current directory
        // Note: In development, .env is copied to output directory by the project file
        // In production, use environment variables or a secure configuration provider
        var envPath = envFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");
        
        if (File.Exists(envPath))
        {
            // Load .env file (DotNetEnv will overwrite existing environment variables)
            DotNetEnv.Env.Load(envPath);
            if (!_suppressConsoleOutput)
                Console.WriteLine("✓ Loaded configuration from .env file");
        }
        else
        {
            if (!_suppressConsoleOutput)
                Console.WriteLine("⚠ No .env file found, using environment variables");
        }

        var config = new AppConfig();

        // Load OpenAI Configuration
        config.OpenAIApiKey = GetEnvironmentVariable("OPENAI_API_KEY", config.OpenAIApiKey);
        config.OpenAIModel = GetEnvironmentVariable("OPENAI_MODEL", config.OpenAIModel);

        // Load File Paths
        config.SummaryInstructionPath = GetEnvironmentVariable("SUMMARY_INSTRUCTION_PATH", config.SummaryInstructionPath);
        config.ReviewInstructionPath = GetEnvironmentVariable("REVIEW_INSTRUCTION_PATH", config.ReviewInstructionPath);
        config.OutputTranscriptDir = GetEnvironmentVariable("OUTPUT_TRANSCRIPT_DIR", config.OutputTranscriptDir);

        // Load Browser Settings
        config.Headless = GetBoolEnvironmentVariable("HEADLESS", config.Headless);
        config.SessionProfile = GetEnvironmentVariable("SESSION_PROFILE", config.SessionProfile);

        // Load Processing Settings
        config.KeepTimestamps = GetBoolEnvironmentVariable("KEEP_TIMESTAMPS", config.KeepTimestamps);
        config.MaxScrollRounds = GetIntEnvironmentVariable("MAX_SCROLL_ROUNDS", config.MaxScrollRounds);
        config.SinglePassThreshold = GetIntEnvironmentVariable("SINGLE_PASS_THRESHOLD", config.SinglePassThreshold);

        // Load Workflow Control Settings
        config.EnableScraping = GetBoolEnvironmentVariable("ENABLE_SCRAPING", config.EnableScraping);
        config.EnableAIProcessing = GetBoolEnvironmentVariable("ENABLE_AI_PROCESSING", config.EnableAIProcessing);

        // Load AI Processing Settings
        config.GenerateCourseSummary = GetBoolEnvironmentVariable("GENERATE_COURSE_SUMMARY", config.GenerateCourseSummary);
        config.GenerateLessonSummaries = GetBoolEnvironmentVariable("GENERATE_LESSON_SUMMARIES", config.GenerateLessonSummaries);
        config.GenerateReview = GetBoolEnvironmentVariable("GENERATE_REVIEW", config.GenerateReview);
        config.MapChunkSize = GetIntEnvironmentVariable("MAP_CHUNK_SIZE", config.MapChunkSize);
        config.MapChunkOverlap = GetIntEnvironmentVariable("MAP_CHUNK_OVERLAP", config.MapChunkOverlap);

        // Load HTML Generation Settings
        config.GenerateHtml = GetBoolEnvironmentVariable("GENERATE_HTML", config.GenerateHtml);
        config.HtmlTheme = GetEnvironmentVariable("HTML_THEME", config.HtmlTheme);

        return config;
    }

    protected virtual string GetEnvironmentVariable(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return !string.IsNullOrWhiteSpace(value) ? value : defaultValue;
    }

    protected virtual bool GetBoolEnvironmentVariable(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return value.ToLower() switch
        {
            "true" => true,
            "1" => true,
            "yes" => true,
            "false" => false,
            "0" => false,
            "no" => false,
            _ => defaultValue
        };
    }

    protected virtual int GetIntEnvironmentVariable(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public void PrintConfiguration()
    {
        Console.WriteLine("\n=== Current Configuration ===");
        Console.WriteLine($"OpenAI Model: {_config.OpenAIModel}");
        Console.WriteLine($"OpenAI API Key: {MaskApiKey(_config.OpenAIApiKey)}");
        Console.WriteLine($"Output Directory: {_config.OutputTranscriptDir}");
        Console.WriteLine($"Session Profile: {_config.SessionProfile}");
        Console.WriteLine($"Headless Mode: {_config.Headless}");
        Console.WriteLine($"Keep Timestamps: {_config.KeepTimestamps}");
        Console.WriteLine($"Max Scroll Rounds: {_config.MaxScrollRounds}");
        Console.WriteLine($"Single Pass Threshold: {_config.SinglePassThreshold}");
        Console.WriteLine($"Enable Scraping: {_config.EnableScraping}");
        Console.WriteLine($"Enable AI Processing: {_config.EnableAIProcessing}");
        Console.WriteLine($"Generate Course Summary: {_config.GenerateCourseSummary}");
        Console.WriteLine($"Generate Lesson Summaries: {_config.GenerateLessonSummaries}");
        Console.WriteLine($"Generate Review: {_config.GenerateReview}");
        Console.WriteLine($"Map Chunk Size: {_config.MapChunkSize}");
        Console.WriteLine($"Map Chunk Overlap: {_config.MapChunkOverlap}");
        Console.WriteLine($"Summary Instruction Path: {_config.SummaryInstructionPath}");
        Console.WriteLine($"Review Instruction Path: {_config.ReviewInstructionPath}");
        Console.WriteLine($"Generate HTML: {_config.GenerateHtml}");
        Console.WriteLine($"HTML Theme: {_config.HtmlTheme}");
        Console.WriteLine("=============================\n");
    }

    private string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "[NOT SET]";
        
        if (apiKey.Length <= 8)
            return "***";

        return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
    }
}