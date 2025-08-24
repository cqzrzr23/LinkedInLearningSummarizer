namespace LinkedInLearningSummarizer.Models;

public class AppConfig
{
    // OpenAI Configuration
    public string OpenAIApiKey { get; set; } = string.Empty;
    public string OpenAIModel { get; set; } = "gpt-4o-mini";

    // File Paths
    public string SummaryInstructionPath { get; set; } = "./prompts/summary_instruction.md";
    public string ReviewInstructionPath { get; set; } = "./prompts/review_instruction.md";
    public string OutputTranscriptDir { get; set; } = "./output";

    // Browser Settings
    public bool Headless { get; set; } = true;
    public string SessionProfile { get; set; } = "linkedin_session";

    // Processing Settings
    public bool KeepTimestamps { get; set; } = false;
    public int MaxScrollRounds { get; set; } = 10;
    public int SinglePassThreshold { get; set; } = 5000;

    // Workflow Control
    public bool EnableScraping { get; set; } = true;
    public bool EnableAIProcessing { get; set; } = true;

    // AI Processing
    public bool GenerateCourseSummary { get; set; } = true;
    public bool GenerateLessonSummaries { get; set; } = false;
    public bool GenerateReview { get; set; } = true;
    public int MapChunkSize { get; set; } = 4000;
    public int MapChunkOverlap { get; set; } = 200;

    // HTML Generation
    public bool GenerateHtml { get; set; } = true;
    public string HtmlTheme { get; set; } = "light"; // light, dark, auto

    public void Validate()
    {
        var errors = new List<string>();

        if (EnableAIProcessing && string.IsNullOrWhiteSpace(OpenAIApiKey))
            errors.Add("OPENAI_API_KEY is required when ENABLE_AI_PROCESSING is true");

        if (string.IsNullOrWhiteSpace(OpenAIModel))
            errors.Add("OPENAI_MODEL is required");

        if (string.IsNullOrWhiteSpace(OutputTranscriptDir))
            errors.Add("OUTPUT_TRANSCRIPT_DIR is required");

        if (string.IsNullOrWhiteSpace(SessionProfile))
            errors.Add("SESSION_PROFILE is required");

        if (MaxScrollRounds <= 0)
            errors.Add("MAX_SCROLL_ROUNDS must be greater than 0");

        if (SinglePassThreshold <= 0)
            errors.Add("SINGLE_PASS_THRESHOLD must be greater than 0");

        if (MapChunkSize <= 0)
            errors.Add("MAP_CHUNK_SIZE must be greater than 0");

        if (MapChunkOverlap < 0)
            errors.Add("MAP_CHUNK_OVERLAP must be 0 or greater");

        if (MapChunkOverlap >= MapChunkSize)
            errors.Add("MAP_CHUNK_OVERLAP must be less than MAP_CHUNK_SIZE");

        if (errors.Any())
        {
            throw new InvalidOperationException(
                "Configuration validation failed:\n" + string.Join("\n", errors));
        }
    }
}