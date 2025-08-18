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
                ShowHelp();
                return 0;
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
                    
                    // TODO: Generate markdown files and AI summaries
                    Console.WriteLine("  → Markdown generation: Coming next phase");
                    Console.WriteLine("  → AI summarization: Coming next phase");
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
    }
}