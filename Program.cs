using LinkedInLearningSummarizer.Services;
using LinkedInLearningSummarizer.Utils;

namespace LinkedInLearningSummarizer;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("LinkedIn Learning AI Course Summarizer");
        Console.WriteLine("======================================\n");

        try
        {
            // Load and validate configuration
            var configService = new ConfigurationService();
            var config = configService.Config;

            // Check for command line arguments
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "--debug":
                        {
                            // AI-only mode with debug (single lesson only)
                            if (!config.EnableAIProcessing)
                            {
                                Console.WriteLine("❌ Debug mode requires ENABLE_AI_PROCESSING=true");
                                return 1;
                            }
                            return await RunAIDebugMode(config);
                        }
                        
                    case "--check-config":
                        {
                            Console.WriteLine("Checking configuration...\n");
                            configService.PrintConfiguration();
                            config.Validate();
                            Console.WriteLine("✓ Configuration is valid!");
                            
                            // Check session status
                            Console.WriteLine("\nChecking LinkedIn session...");
                            using var scraper = new LinkedInScraper(config);
                            await scraper.InitializeBrowserAsync();
                            var hasValidSession = await scraper.HasValidSessionAsync();
                            
                            if (hasValidSession)
                            {
                                Console.WriteLine("✓ LinkedIn session is valid and active!");
                            }
                            else
                            {
                                Console.WriteLine("⚠ No valid LinkedIn session found.");
                                Console.WriteLine("  Run the application with a URLs file to authenticate.");
                            }
                            
                            return 0;
                        }

                    case "--reset-session":
                        Console.WriteLine("Resetting LinkedIn session...");
                        var sessionPath = Path.Combine(Directory.GetCurrentDirectory(), config.SessionProfile);
                        if (Directory.Exists(sessionPath))
                        {
                            Directory.Delete(sessionPath, true);
                            Console.WriteLine("✓ Session reset successfully!");
                        }
                        else
                        {
                            Console.WriteLine("No existing session found.");
                        }
                        return 0;

                    case "--test":
                        Console.WriteLine("Running transcript extraction test with test-urls.txt...");
                        return await RunTranscriptTest(config);


                    case "--help":
                    case "-h":
                        ShowHelp();
                        return 0;

                    default:
                        // Assume it's a file path for URLs
                        return await ProcessUrlsFromFile(args[0], config, configService);
                }
            }
            else
            {
                // No command line arguments - check configuration
                if (!config.EnableScraping && config.EnableAIProcessing)
                {
                    // AI-only mode: Process existing courses from output directory
                    return await RunAIOnlyMode(config);
                }
                else
                {
                    // Default behavior: Show help when scraping is enabled but no URL file provided
                    ShowHelp();
                    return 0;
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"\n❌ Configuration Error:\n{ex.Message}");
            Console.WriteLine("\nPlease check your .env file or environment variables.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Unexpected Error: {ex.Message}");
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            return 1;
        }
    }

    static async Task<int> RunTranscriptTest(LinkedInLearningSummarizer.Models.AppConfig config)
    {
        var testUrlsFile = "test-urls.txt";
        
        if (!File.Exists(testUrlsFile))
        {
            Console.WriteLine($"Error: {testUrlsFile} not found. Please create this file with LinkedIn Learning course URLs.");
            return 1;
        }

        Console.WriteLine($"📋 Reading test URLs from: {testUrlsFile}");
        
        // Process URLs from test file
        var fileResult = await UrlFileProcessor.ProcessUrlFileAsync(testUrlsFile);
        
        if (!fileResult.IsSuccess)
        {
            Console.WriteLine($"Error: {fileResult.ErrorMessage}");
            return 1;
        }

        if (!fileResult.Urls.Any())
        {
            Console.WriteLine("No valid URLs found in the test file. Please add LinkedIn Learning course URLs.");
            return 0;
        }

        Console.WriteLine($"🎯 Found {fileResult.ValidUrlCount} course(s) to test:");
        foreach (var url in fileResult.Urls)
        {
            Console.WriteLine($"  • {url}");
        }

        // Initialize LinkedIn scraper
        using var scraper = new LinkedInScraper(config);
        
        try
        {
            // Ensure we're authenticated
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("🔐 AUTHENTICATION");
            Console.WriteLine(new string('=', 70));
            
            await scraper.EnsureAuthenticatedAsync();
            
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("🎬 TRANSCRIPT EXTRACTION TEST");
            Console.WriteLine(new string('=', 70));

            // Process each course
            for (int i = 0; i < fileResult.Urls.Count; i++)
            {
                var url = fileResult.Urls[i];
                Console.WriteLine($"\n🎓 --- Course {i + 1} of {fileResult.Urls.Count} ---");
                
                try
                {
                    // Extract course metadata and lessons
                    var course = await scraper.ProcessCourseAsync(url);
                    Console.WriteLine($"✓ Course: {course.Title}");
                    Console.WriteLine($"✓ Found {course.Lessons.Count} lessons");
                    
                    // Extract transcripts from all lessons
                    Console.WriteLine($"\n📝 Extracting transcripts from {course.Lessons.Count} lessons...");
                    await scraper.ProcessLessonTranscriptsAsync(course.Lessons);
                    
                    // Generate markdown files with AI integration
                    OpenAIService? openAIService = null;
                    if (config.EnableAIProcessing && (config.GenerateCourseSummary || config.GenerateLessonSummaries || config.GenerateReview))
                    {
                        openAIService = new OpenAIService(config);
                    }
                    
                    var markdownGenerator = new LinkedInLearningSummarizer.Services.MarkdownGenerator(config, openAIService);
                    await markdownGenerator.GenerateAsync(course);
                    
                    // Generate HTML files if enabled
                    if (config.GenerateHtml)
                    {
                        var htmlGenerator = new LinkedInLearningSummarizer.Services.HtmlGenerator(config);
                        await htmlGenerator.GenerateAsync(course);
                    }
                    
                    // Summary for this course
                    var transcriptCount = course.Lessons.Count(l => l.HasTranscript);
                    Console.WriteLine($"\n📊 Course Summary:");
                    Console.WriteLine($"  • Total lessons: {course.Lessons.Count}");
                    Console.WriteLine($"  • With transcripts: {transcriptCount}");
                    Console.WriteLine($"  • Success rate: {(double)transcriptCount / course.Lessons.Count:P0}");
                    Console.WriteLine($"  • Output directory: {config.OutputTranscriptDir}/[course-name]/");
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to process course {url}: {ex.Message}");
                    // Continue with next course
                }
            }

            Console.WriteLine($"\n🎉 Completed transcript extraction and markdown generation!");
            Console.WriteLine($"📁 Check generated files in: {config.OutputTranscriptDir}/[course-name]/");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error during transcript test: {ex.Message}");
            return 1;
        }
    }


    static async Task<int> ProcessUrlsFromFile(string filePath, LinkedInLearningSummarizer.Models.AppConfig config, ConfigurationService configService)
    {
        Console.WriteLine($"Processing URLs from: {filePath}");
        
        // Validate configuration first
        configService.PrintConfiguration();
        config.Validate();
        Console.WriteLine("✓ Configuration validated successfully!\n");

        // Process URLs from file using UrlFileProcessor
        var fileResult = await UrlFileProcessor.ProcessUrlFileAsync(filePath);
        
        if (!fileResult.IsSuccess)
        {
            Console.WriteLine($"Error: {fileResult.ErrorMessage}");
            return 1;
        }

        if (!fileResult.Urls.Any())
        {
            Console.WriteLine("No valid URLs found in the file.");
            return 0;
        }

        Console.WriteLine($"Found {fileResult.ValidUrlCount} URL(s) to process:\n");
        foreach (var url in fileResult.Urls)
        {
            Console.WriteLine($"  • {url}");
        }

        // Initialize LinkedIn scraper
        using var scraper = new LinkedInScraper(config);
        
        try
        {
            // Ensure we're authenticated (handles first-run login and session restoration)
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("AUTHENTICATION");
            Console.WriteLine(new string('=', 60));
            
            await scraper.EnsureAuthenticatedAsync();
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("PROCESSING COURSES");
            Console.WriteLine(new string('=', 60));

            // Process each course
            for (int i = 0; i < fileResult.Urls.Count; i++)
            {
                var url = fileResult.Urls[i];
                Console.WriteLine($"\n--- Course {i + 1} of {fileResult.Urls.Count} ---");
                
                try
                {
                    var course = await scraper.ProcessCourseAsync(url);
                    Console.WriteLine($"✓ Processed: {course.Title}");
                    
                    // Extract transcripts from all lessons
                    await scraper.ProcessLessonTranscriptsAsync(course.Lessons);
                    
                    // Generate markdown files with AI integration
                    OpenAIService? openAIService = null;
                    if (config.EnableAIProcessing && (config.GenerateCourseSummary || config.GenerateLessonSummaries || config.GenerateReview))
                    {
                        openAIService = new OpenAIService(config);
                    }
                    
                    var markdownGenerator = new LinkedInLearningSummarizer.Services.MarkdownGenerator(config, openAIService);
                    await markdownGenerator.GenerateAsync(course);
                    
                    // Generate HTML files if enabled
                    if (config.GenerateHtml)
                    {
                        var htmlGenerator = new LinkedInLearningSummarizer.Services.HtmlGenerator(config);
                        await htmlGenerator.GenerateAsync(course);
                    }
                    
                    Console.WriteLine("  → Markdown files generated successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to process course {url}: {ex.Message}");
                    // Continue with next course
                }
            }

            Console.WriteLine($"\n✓ Completed processing {fileResult.Urls.Count} course(s)!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error during course processing: {ex.Message}");
            return 1;
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  LinkedInLearningSummarizer <urls.txt>        Process courses from URL file");
        Console.WriteLine("  LinkedInLearningSummarizer --test            Test transcript extraction with test-urls.txt");
        Console.WriteLine("  LinkedInLearningSummarizer --debug           Debug AI processing with single lesson");
        Console.WriteLine("  LinkedInLearningSummarizer --check-config    Validate configuration");
        Console.WriteLine("  LinkedInLearningSummarizer --reset-session   Clear saved LinkedIn session");
        Console.WriteLine("  LinkedInLearningSummarizer --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  Create a .env file based on .env.example");
        Console.WriteLine("  Or set environment variables directly");
        Console.WriteLine();
        Console.WriteLine("URL File Format:");
        Console.WriteLine("  One LinkedIn Learning course URL per line");
        Console.WriteLine("  Lines starting with # are ignored (comments)");
        Console.WriteLine("  Empty lines are ignored");
        Console.WriteLine();
        Console.WriteLine("Testing:");
        Console.WriteLine("  Add course URLs to test-urls.txt and run --test");
        Console.WriteLine("  Generated markdown files will be saved to output/[course-name]/");
    }

    static async Task<int> RunAIOnlyMode(LinkedInLearningSummarizer.Models.AppConfig config)
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("🤖 AI-ONLY MODE");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("Scanning for existing courses to process...");

        if (!Directory.Exists(config.OutputTranscriptDir))
        {
            Console.WriteLine($"❌ Output directory not found: {config.OutputTranscriptDir}");
            Console.WriteLine("Please run with ENABLE_SCRAPING=true first to extract some courses.");
            return 1;
        }

        // Find existing course directories
        var courseDirectories = Directory.GetDirectories(config.OutputTranscriptDir)
            .Where(dir => File.Exists(Path.Combine(dir, "README.md")))
            .ToList();

        if (!courseDirectories.Any())
        {
            Console.WriteLine($"❌ No existing courses found in: {config.OutputTranscriptDir}");
            Console.WriteLine("Please run with ENABLE_SCRAPING=true first to extract some courses.");
            return 1;
        }

        Console.WriteLine($"✓ Found {courseDirectories.Count} existing course(s) for AI processing:");

        var successCount = 0;
        var failureCount = 0;

        foreach (var courseDir in courseDirectories)
        {
            var courseName = Path.GetFileName(courseDir);
            Console.WriteLine($"\n📚 Processing: {courseName}");

            try
            {
                // Load existing course data
                var course = await LoadCourseFromDirectory(courseDir);
                
                if (course == null)
                {
                    Console.WriteLine($"  ❌ Failed to load course data from {courseDir}");
                    failureCount++;
                    continue;
                }

                Console.WriteLine($"  ✓ Loaded: {course.Title}");
                Console.WriteLine($"  ✓ Found {course.Lessons.Count} lessons with transcripts");

                // Initialize AI service for processing
                OpenAIService? openAIService = null;
                if (config.GenerateCourseSummary || config.GenerateLessonSummaries || config.GenerateReview)
                {
                    openAIService = new OpenAIService(config);
                }

                // Generate AI-enhanced markdown files
                var markdownGenerator = new LinkedInLearningSummarizer.Services.MarkdownGenerator(config, openAIService);
                await markdownGenerator.GenerateAsync(course);
                
                // Generate HTML files if enabled
                if (config.GenerateHtml)
                {
                    var htmlGenerator = new LinkedInLearningSummarizer.Services.HtmlGenerator(config);
                    await htmlGenerator.GenerateAsync(course);
                }

                Console.WriteLine($"  ✅ AI processing complete for: {course.Title}");
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Failed to process {courseName}: {ex.Message}");
                failureCount++;
            }
        }

        // Summary
        Console.WriteLine($"\n" + new string('=', 60));
        Console.WriteLine("📊 AI PROCESSING SUMMARY");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"✅ Successfully processed: {successCount} courses");
        Console.WriteLine($"❌ Failed: {failureCount} courses");
        Console.WriteLine($"📁 Output directory: {config.OutputTranscriptDir}");

        if (successCount > 0)
        {
            Console.WriteLine("\n🔍 Generated files:");
            if (config.GenerateCourseSummary)
                Console.WriteLine("  • ai_summary.md - AI-generated course summaries");
            if (config.GenerateReview)
                Console.WriteLine("  • ai_review.md - AI-generated course reviews");
            if (config.GenerateLessonSummaries)
                Console.WriteLine("  • Enhanced lesson files with AI summaries");
        }

        return failureCount > 0 ? 1 : 0;
    }

    static async Task<int> RunAIDebugMode(LinkedInLearningSummarizer.Models.AppConfig config)
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("🐛 AI DEBUG MODE - Single Lesson Test");
        Console.WriteLine(new string('=', 60));

        if (!Directory.Exists(config.OutputTranscriptDir))
        {
            Console.WriteLine($"❌ Output directory not found: {config.OutputTranscriptDir}");
            return 1;
        }

        // Find first course directory
        var courseDir = Directory.GetDirectories(config.OutputTranscriptDir)
            .FirstOrDefault(dir => File.Exists(Path.Combine(dir, "README.md")));

        if (courseDir == null)
        {
            Console.WriteLine($"❌ No existing courses found in: {config.OutputTranscriptDir}");
            return 1;
        }

        Console.WriteLine($"📚 Using course: {Path.GetFileName(courseDir)}");

        // Load only the first lesson for testing
        var lessonsDir = Path.Combine(courseDir, "lessons");
        var firstLessonFile = Directory.GetFiles(lessonsDir, "*.md").OrderBy(f => f).FirstOrDefault();

        if (firstLessonFile == null)
        {
            Console.WriteLine("❌ No lesson files found");
            return 1;
        }

        Console.WriteLine($"📖 Testing with: {Path.GetFileName(firstLessonFile)}");

        try
        {
            var lesson = await ParseLessonFromFile(firstLessonFile);
            if (lesson == null || string.IsNullOrWhiteSpace(lesson.Transcript))
            {
                Console.WriteLine("❌ Failed to load lesson transcript");
                return 1;
            }

            Console.WriteLine($"✓ Loaded lesson: {lesson.Title}");
            Console.WriteLine($"✓ Transcript length: {lesson.Transcript.Length:N0} characters");

            // Test AI processing with just this lesson
            var openAIService = new OpenAIService(config);

            if (config.GenerateLessonSummaries)
            {
                Console.WriteLine("\n🤖 Testing lesson summary generation...");
                var summary = await openAIService.GenerateLessonSummaryAsync(lesson.Transcript);
                Console.WriteLine($"✅ Lesson summary generated ({summary.Length} characters)");
                Console.WriteLine($"Preview: {summary.Substring(0, Math.Min(200, summary.Length))}...");
            }

            Console.WriteLine("\n✅ Debug test completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Debug test failed: {ex.Message}");
            return 1;
        }
    }

    static async Task<LinkedInLearningSummarizer.Models.Course?> LoadCourseFromDirectory(string courseDir)
    {
        try
        {
            var readmePath = Path.Combine(courseDir, "README.md");
            var lessonsDir = Path.Combine(courseDir, "lessons");

            if (!File.Exists(readmePath) || !Directory.Exists(lessonsDir))
            {
                return null;
            }

            // Parse README.md for course metadata
            var readmeContent = await File.ReadAllTextAsync(readmePath);
            var course = ParseCourseFromReadme(readmeContent, courseDir);

            // Load lessons from lessons directory
            var lessonFiles = Directory.GetFiles(lessonsDir, "*.md")
                .OrderBy(f => f)
                .ToList();

            foreach (var lessonFile in lessonFiles)
            {
                var lesson = await ParseLessonFromFile(lessonFile);
                if (lesson != null)
                {
                    course.Lessons.Add(lesson);
                }
            }

            return course;
        }
        catch
        {
            return null;
        }
    }

    static LinkedInLearningSummarizer.Models.Course ParseCourseFromReadme(string readmeContent, string courseDir)
    {
        var course = new LinkedInLearningSummarizer.Models.Course();
        
        // Extract course title (first H1)
        var lines = readmeContent.Split('\n');
        var titleLine = lines.FirstOrDefault(l => l.StartsWith("# "));
        course.Title = titleLine?.Substring(2).Trim() ?? Path.GetFileName(courseDir);

        // Extract instructor
        var instructorLine = lines.FirstOrDefault(l => l.StartsWith("**Instructor:**"));
        course.Instructor = instructorLine?.Split(':')[1].Trim() ?? "Unknown";

        // Set basic properties
        course.Url = $"https://linkedin.com/learning/{Path.GetFileName(courseDir)}";
        course.TotalLessons = 0; // Will be set based on lessons loaded

        return course;
    }

    static async Task<LinkedInLearningSummarizer.Models.Lesson?> ParseLessonFromFile(string lessonFile)
    {
        try
        {
            var content = await File.ReadAllTextAsync(lessonFile);
            var lines = content.Split('\n');

            var lesson = new LinkedInLearningSummarizer.Models.Lesson();

            // Extract lesson title (first H1)
            var titleLine = lines.FirstOrDefault(l => l.StartsWith("# "));
            if (titleLine != null)
            {
                var title = titleLine.Substring(2).Trim();
                // Parse "Lesson X: Title" format
                var colonIndex = title.IndexOf(':');
                if (colonIndex > 0 && title.StartsWith("Lesson "))
                {
                    var lessonNumberStr = title.Substring(7, colonIndex - 7).Trim();
                    if (int.TryParse(lessonNumberStr, out var lessonNumber))
                    {
                        lesson.LessonNumber = lessonNumber;
                        lesson.Title = title.Substring(colonIndex + 1).Trim();
                    }
                }
                else
                {
                    lesson.Title = title;
                    lesson.LessonNumber = 0;
                }
            }

            // Extract transcript (content after "## Transcript")
            var transcriptIndex = Array.FindIndex(lines, l => l.Trim() == "## Transcript");
            if (transcriptIndex >= 0 && transcriptIndex + 2 < lines.Length)
            {
                var transcriptLines = lines.Skip(transcriptIndex + 2).ToList();
                lesson.Transcript = string.Join("\n", transcriptLines).Trim();
                lesson.HasTranscript = !string.IsNullOrWhiteSpace(lesson.Transcript);
            }

            // Set extracted date
            lesson.ExtractedAt = File.GetLastWriteTime(lessonFile);

            return lesson.HasTranscript ? lesson : null;
        }
        catch
        {
            return null;
        }
    }
}