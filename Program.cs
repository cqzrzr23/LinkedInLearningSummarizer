using LinkedInLearningSummarizer.Services;

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
                        Console.WriteLine("Checking configuration...\n");
                        configService.PrintConfiguration();
                        config.Validate();
                        Console.WriteLine("✓ Configuration is valid!");
                        return 0;

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
                        if (File.Exists(args[0]))
                        {
                            await ProcessUrlsFromFile(args[0], config, configService);
                        }
                        else
                        {
                            Console.WriteLine($"Error: File not found: {args[0]}");
                            return 1;
                        }
                        break;
                }
            }
            else
            {
                ShowHelp();
                return 0;
            }

            return 0;
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

    static async Task ProcessUrlsFromFile(string filePath, LinkedInLearningSummarizer.Models.AppConfig config, ConfigurationService configService)
    {
        Console.WriteLine($"Processing URLs from: {filePath}");
        
        // Validate configuration first
        configService.PrintConfiguration();
        config.Validate();
        Console.WriteLine("✓ Configuration validated successfully!\n");

        // Read URLs from file
        var urls = await File.ReadAllLinesAsync(filePath);
        var validUrls = urls
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();

        if (!validUrls.Any())
        {
            Console.WriteLine("No valid URLs found in the file.");
            return;
        }

        Console.WriteLine($"Found {validUrls.Count} URL(s) to process:\n");
        foreach (var url in validUrls)
        {
            Console.WriteLine($"  • {url}");
        }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("TODO: LinkedIn scraping and processing will be implemented next");
        Console.WriteLine("This will include:");
        Console.WriteLine("  1. Session management");
        Console.WriteLine("  2. Course navigation");
        Console.WriteLine("  3. Transcript extraction");
        Console.WriteLine("  4. Markdown generation");
        Console.WriteLine("  5. AI summarization");
        Console.WriteLine(new string('=', 50));
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