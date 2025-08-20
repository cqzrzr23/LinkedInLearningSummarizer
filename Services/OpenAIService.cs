using LinkedInLearningSummarizer.Models;
using OpenAI.Chat;
using System.Text;
using System.Text.RegularExpressions;

namespace LinkedInLearningSummarizer.Services;

public class OpenAIService
{
    private readonly AppConfig _config;
    private readonly ChatClient _chatClient;
    private DateTime _lastApiCall = DateTime.MinValue;
    private readonly int _rateLimitDelayMs = 100; // Minimum delay between API calls
    
    // Cached instruction content
    private string? _summaryInstructions;
    private string? _reviewInstructions;

    public OpenAIService(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        if (string.IsNullOrWhiteSpace(_config.OpenAIApiKey))
            throw new InvalidOperationException("OpenAI API key is required but not configured");
            
        _chatClient = new ChatClient(_config.OpenAIModel, _config.OpenAIApiKey);
        
        Console.WriteLine($"✓ OpenAI service initialized with model: {_config.OpenAIModel}");
    }

    /// <summary>
    /// Generates an AI summary for a lesson transcript
    /// </summary>
    public async Task<string> GenerateLessonSummaryAsync(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return string.Empty;

        var instructions = await LoadSummaryInstructionsAsync();
        var prompt = $"Please summarize this lesson transcript:\n\n{transcript}";
        
        return await CallOpenAIWithRetryAsync(prompt, instructions);
    }

    /// <summary>
    /// Generates a comprehensive course summary from full transcript
    /// </summary>
    public async Task<string> GenerateCourseSummaryAsync(string fullTranscript)
    {
        if (string.IsNullOrWhiteSpace(fullTranscript))
            return string.Empty;

        var instructions = await LoadSummaryInstructionsAsync();
        
        // For very long transcripts, chunk them
        if (fullTranscript.Length > _config.MapChunkSize)
        {
            return await GenerateChunkedSummaryAsync(fullTranscript, instructions);
        }
        
        var prompt = $"Please create a comprehensive course summary from this complete transcript:\n\n{fullTranscript}";
        return await CallOpenAIWithRetryAsync(prompt, instructions);
    }

    /// <summary>
    /// Generates a structured course review using the review template
    /// </summary>
    public async Task<string> GenerateCourseReviewAsync(Course course)
    {
        if (course == null)
            return string.Empty;

        if (!_config.GenerateReview)
            return string.Empty;

        var instructions = await LoadReviewInstructionsAsync();
        
        // Build course context for review
        var courseContext = BuildCourseContextForReview(course);
        var prompt = $"Please review this LinkedIn Learning course:\n\n{courseContext}";
        
        return await CallOpenAIWithRetryAsync(prompt, instructions);
    }

    /// <summary>
    /// Loads summary instructions from file with fallback
    /// </summary>
    private Task<string> LoadSummaryInstructionsAsync()
    {
        if (_summaryInstructions != null)
            return Task.FromResult(_summaryInstructions);

        const string defaultInstructions = @"
You are an expert course summarizer. Create a clear, structured, and concise summary of the course content.

## Format
Your output must be in Markdown with these sections:

### Course Summary
- 8-12 concise bullets capturing main learning outcomes
- Focus on practical skills and actionable takeaways

### Key Skills & Tools
- List specific tools, technologies, or frameworks covered
- Include practical applications

### Key Terminology
- Important terms with brief definitions
- Domain-specific concepts

### Practical Takeaways
- 6-10 actionable steps learners can implement
- Real-world applications

## Guidelines
- Use clear, professional language
- Be faithful to the content - don't add outside knowledge
- Keep under 800 words
- Use bullet points for readability
";

        _summaryInstructions = LoadInstructionFile(_config.SummaryInstructionPath, defaultInstructions);
        return Task.FromResult(_summaryInstructions);
    }

    /// <summary>
    /// Loads review instructions from file with fallback
    /// </summary>
    private Task<string> LoadReviewInstructionsAsync()
    {
        if (_reviewInstructions != null)
            return Task.FromResult(_reviewInstructions);

        const string defaultInstructions = @"
You are an expert course reviewer. Review this LinkedIn Learning course and provide scores.

Score each category 1-5 (1=poor, 5=excellent):
1. Course Scope & Coverage
2. Clarity & Teaching Style  
3. Engagement & Delivery
4. Practical Application
5. Learning Outcomes & Accuracy

## Output Format (Markdown):

# Course Review: [Course Title]

## 1. Course Scope & Coverage
**Score: X/5**
[Evaluation]

## 2. Clarity & Teaching Style
**Score: X/5**
[Evaluation]

## 3. Engagement & Delivery
**Score: X/5**
[Evaluation]

## 4. Practical Application
**Score: X/5**
[Evaluation]

## 5. Learning Outcomes & Accuracy
**Score: X/5**
[Evaluation]

## ✅ Strengths
- [2-4 strengths]

## ⚠️ Weaknesses
- [2-4 improvements]

## 👥 Best Audience
- [Target learner description]

## 🎯 Final Review Score
**X.X / 5** (average of above scores)
[One-sentence assessment]
";

        _reviewInstructions = LoadInstructionFile(_config.ReviewInstructionPath, defaultInstructions);
        return Task.FromResult(_reviewInstructions);
    }

    /// <summary>
    /// Loads instruction file content with fallback to default
    /// </summary>
    private string LoadInstructionFile(string filePath, string defaultContent)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine($"✓ Loaded AI instructions from: {filePath}");
                    return content;
                }
            }
            
            Console.WriteLine($"⚠ Instruction file not found: {filePath}, using default instructions");
            return defaultContent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error loading instruction file {filePath}: {ex.Message}");
            Console.WriteLine("Using default instructions");
            return defaultContent;
        }
    }

    /// <summary>
    /// Calls OpenAI API with retry logic and rate limiting
    /// </summary>
    private async Task<string> CallOpenAIWithRetryAsync(string prompt, string instructions, int maxRetries = 3)
    {
        var attempts = 0;
        var estimatedTokens = EstimateTokens(prompt + instructions);
        
        // Validate content size before processing
        if (estimatedTokens > 100000)
        {
            Console.WriteLine($"⚠ Warning: Large content detected ({estimatedTokens:N0} tokens). This may take several minutes...");
        }
        
        while (attempts < maxRetries)
        {
            try
            {
                await ApplyRateLimitAsync();
                
                Console.WriteLine($"  → Sending request to OpenAI (attempt {attempts + 1}/{maxRetries}, ~{estimatedTokens:N0} tokens)...");
                
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(instructions),
                    new UserChatMessage(prompt)
                };

                var options = new ChatCompletionOptions()
                {
                    MaxOutputTokenCount = 4000,
                    Temperature = 0.3f  // Lower temperature for more consistent output
                };

                // Add timeout to prevent infinite hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // 5-minute timeout
                var response = await _chatClient.CompleteChatAsync(messages, options, cts.Token);
                
                if (response?.Value?.Content?.Count > 0)
                {
                    var content = response.Value.Content[0].Text;
                    Console.WriteLine($"✓ AI processing completed ({EstimateTokens(prompt + instructions + content)} tokens)");
                    return content;
                }
                
                throw new Exception("No response content received from OpenAI");
            }
            catch (OperationCanceledException)
            {
                attempts++;
                Console.WriteLine($"⚠ OpenAI API attempt {attempts}/{maxRetries} timed out after 5 minutes");
                
                if (attempts >= maxRetries)
                {
                    Console.WriteLine($"❌ OpenAI API failed after {maxRetries} timeout attempts");
                    return GenerateErrorFallback("Request timed out - content may be too large");
                }
                
                Console.WriteLine("  → Retrying with shorter content...");
                // Reduce content size for retry
                if (prompt.Length > 10000)
                {
                    prompt = prompt.Substring(0, 10000) + "\n\n[Content truncated due to timeout]";
                    Console.WriteLine("  → Content truncated for retry");
                }
            }
            catch (Exception ex)
            {
                attempts++;
                var errorType = ex.GetType().Name;
                Console.WriteLine($"⚠ OpenAI API attempt {attempts}/{maxRetries} failed ({errorType}): {ex.Message}");
                
                if (attempts >= maxRetries)
                {
                    Console.WriteLine($"❌ OpenAI API failed after {maxRetries} attempts");
                    return GenerateErrorFallback(ex.Message);
                }
                
                // Exponential backoff
                await Task.Delay(Math.Min(1000 * (int)Math.Pow(2, attempts), 10000));
            }
        }
        
        return GenerateErrorFallback("Maximum retry attempts exceeded");
    }

    /// <summary>
    /// Handles long transcripts by chunking and using map-reduce approach
    /// </summary>
    private async Task<string> GenerateChunkedSummaryAsync(string fullTranscript, string instructions)
    {
        Console.WriteLine($"📊 Processing long transcript ({fullTranscript.Length:N0} chars) with chunking...");
        Console.WriteLine($"🔧 DEBUG: About to call ChunkText method...");
        
        var chunks = ChunkText(fullTranscript, _config.MapChunkSize, _config.MapChunkOverlap);
        Console.WriteLine($"🔧 DEBUG: ChunkText completed successfully!");
        
        var chunkSummaries = new List<string>();
        
        Console.WriteLine($"  → Split into {chunks.Count} chunks (size: {_config.MapChunkSize:N0}, overlap: {_config.MapChunkOverlap:N0})");
        
        var processedChars = 0;
        var startTime = DateTime.Now;
        
        // Process each chunk
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkLength = chunks[i].Length;
            var progressPercent = (double)(i + 1) / chunks.Count * 100;
            
            Console.WriteLine($"  → Processing chunk {i + 1}/{chunks.Count} ({chunkLength:N0} chars, {progressPercent:F0}% complete)...");
            Console.WriteLine($"🔧 DEBUG: About to call OpenAI API for chunk {i + 1}...");
            
            var chunkPrompt = $"Please summarize this portion of a course transcript (chunk {i + 1} of {chunks.Count}):\n\n{chunks[i]}";
            var chunkSummary = await CallOpenAIWithRetryAsync(chunkPrompt, instructions);
            Console.WriteLine($"🔧 DEBUG: OpenAI API call completed for chunk {i + 1}!");
            chunkSummaries.Add(chunkSummary);
            
            processedChars += chunkLength;
            var elapsed = DateTime.Now - startTime;
            var avgTimePerChunk = elapsed.TotalSeconds / (i + 1);
            var estimatedRemaining = TimeSpan.FromSeconds(avgTimePerChunk * (chunks.Count - i - 1));
            
            Console.WriteLine($"    ✓ Chunk {i + 1} completed ({processedChars:N0}/{fullTranscript.Length:N0} chars processed, ~{estimatedRemaining.TotalMinutes:F0}m remaining)");
        }
        
        // Combine chunk summaries into final summary
        var combinedSummaries = string.Join("\n\n---\n\n", chunkSummaries);
        var finalPrompt = $"Please create a comprehensive course summary from these individual section summaries:\n\n{combinedSummaries}";
        
        var totalElapsed = DateTime.Now - startTime;
        Console.WriteLine($"  → All {chunks.Count} chunks processed in {totalElapsed.TotalMinutes:F1} minutes");
        Console.WriteLine($"  → Combining {chunks.Count} chunk summaries into final course summary...");
        
        var finalResult = await CallOpenAIWithRetryAsync(finalPrompt, instructions);
        
        Console.WriteLine($"  ✅ Course summary generation completed! ({fullTranscript.Length:N0} chars → {finalResult.Length:N0} chars summary)");
        return finalResult;
    }

    /// <summary>
    /// Chunks text into smaller pieces with overlap
    /// </summary>
    private List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        // Input validation
        if (string.IsNullOrEmpty(text))
        {
            Console.WriteLine("WARNING: ChunkText: Empty or null text provided, returning empty list");
            return new List<string>();
        }
        
        // Validate and fix chunk size
        const int minChunkSize = 1000; // Absolute minimum to prevent errors
        const int optimalChunkSize = 350000; // Optimal size for GPT-4o-mini (87.5K tokens)
        
        if (chunkSize <= 0)
        {
            Console.WriteLine($"WARNING: ChunkText: Invalid chunkSize ({chunkSize}), using optimal size ({optimalChunkSize})");
            chunkSize = optimalChunkSize;
        }
        else if (chunkSize < minChunkSize)
        {
            Console.WriteLine($"WARNING: ChunkText: Very small chunkSize ({chunkSize}), using optimal size ({optimalChunkSize})");
            chunkSize = optimalChunkSize;
        }
        else if (chunkSize < optimalChunkSize)
        {
            Console.WriteLine($"WARNING: ChunkText: Suboptimal chunkSize ({chunkSize:N0}). Recommended: {optimalChunkSize:N0} for GPT-4o-mini efficiency");
            Console.WriteLine($"  → Your current size may cause unnecessary chunking and slower processing");
            // Allow user's choice but warn them
        }
        
        // Validate and fix overlap
        if (overlap < 0)
        {
            Console.WriteLine($"WARNING: ChunkText: Negative overlap ({overlap}), setting to 0");
            overlap = 0;
        }
        else if (overlap >= chunkSize)
        {
            var maxOverlap = Math.Max(0, chunkSize - 1);
            Console.WriteLine($"WARNING: ChunkText: Overlap ({overlap}) >= chunkSize ({chunkSize}), capping at {maxOverlap}");
            overlap = maxOverlap;
        }
        
        // If text is shorter than chunk size, return single chunk
        if (text.Length <= chunkSize)
        {
            return new List<string> { text };
        }
        
        var chunks = new List<string>();
        var start = 0;
        
        while (start < text.Length)
        {
            var end = Math.Min(start + chunkSize, text.Length);
            var chunk = text.Substring(start, end - start);
            chunks.Add(chunk);
            
            // If we've reached the end of the text, break
            if (end >= text.Length)
                break;
                
            // Move start position with overlap
            var nextStart = end - overlap;
            
            // Safety check: ensure we always make progress (should not be needed with validation above)
            if (nextStart <= start)
            {
                Console.WriteLine($"WARNING: ChunkText: Safety check triggered - forcing progress from position {start}");
                nextStart = start + 1;
            }
            
            start = nextStart;
        }
        
        return chunks;
    }

    /// <summary>
    /// Builds course context for review generation
    /// </summary>
    private string BuildCourseContextForReview(Course course)
    {
        var context = new StringBuilder();
        
        context.AppendLine($"**Course:** {course.Title}");
        context.AppendLine($"**Instructor:** {course.Instructor}");
        context.AppendLine($"**Total Lessons:** {course.TotalLessons}");
        context.AppendLine($"**Lessons with Transcripts:** {course.Lessons.Count(l => l.HasTranscript)}");
        context.AppendLine();
        
        context.AppendLine("**Table of Contents:**");
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).OrderBy(l => l.LessonNumber).ToList();
        for (int i = 0; i < lessonsWithTranscripts.Count; i++)
        {
            var lesson = lessonsWithTranscripts[i];
            context.AppendLine($"{i + 1}. {lesson.Title}");
        }
        context.AppendLine();
        
        context.AppendLine("**Sample Transcripts:**");
        // Include first few transcripts for review context
        var sampleLessons = lessonsWithTranscripts.Take(3).ToList();
        foreach (var lesson in sampleLessons)
        {
            context.AppendLine($"\n### {lesson.Title}");
            // Include first 500 characters of transcript
            var transcript = lesson.Transcript ?? "";
            if (transcript.Length > 500)
                transcript = transcript.Substring(0, 500) + "...";
            context.AppendLine(transcript);
        }
        
        return context.ToString();
    }

    /// <summary>
    /// Applies rate limiting between API calls
    /// </summary>
    private async Task ApplyRateLimitAsync()
    {
        var timeSinceLastCall = DateTime.Now - _lastApiCall;
        if (timeSinceLastCall.TotalMilliseconds < _rateLimitDelayMs)
        {
            var delay = _rateLimitDelayMs - (int)timeSinceLastCall.TotalMilliseconds;
            await Task.Delay(delay);
        }
        _lastApiCall = DateTime.Now;
    }

    /// <summary>
    /// Estimates token count for cost tracking
    /// </summary>
    private int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token
        return text.Length / 4;
    }

    /// <summary>
    /// Generates fallback content when API fails
    /// </summary>
    private string GenerateErrorFallback(string error)
    {
        return $@"# AI Processing Unavailable

*AI summary could not be generated due to an error:*
> {error}

*The transcript content is available in the lesson files. Please refer to the full transcript for detailed information.*

**Note:** This content was extracted successfully but AI processing failed. All transcript data is preserved in the individual lesson files.";
    }
}