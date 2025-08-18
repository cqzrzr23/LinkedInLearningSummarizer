using Microsoft.Playwright;
using LinkedInLearningSummarizer.Models;

namespace LinkedInLearningSummarizer.Services;

public class LinkedInScraper : IDisposable
{
    private readonly AppConfig _config;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _disposed = false;

    public LinkedInScraper(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task InitializeBrowserAsync()
    {
        if (_playwright != null)
            return; // Already initialized

        Console.WriteLine("Initializing browser...");
        
        _playwright = await Playwright.CreateAsync();
        
        var browserOptions = new BrowserTypeLaunchOptions
        {
            Headless = _config.Headless
        };

        // Use Chromium for better LinkedIn compatibility
        _browser = await _playwright.Chromium.LaunchAsync(browserOptions);
        
        Console.WriteLine($"✓ Browser initialized (Headless: {_config.Headless})");
    }

    public async Task<bool> HasValidSessionAsync()
    {
        var sessionPath = GetSessionPath();
        
        if (!Directory.Exists(sessionPath))
        {
            Console.WriteLine("No existing session found.");
            return false;
        }

        try
        {
            // Try to load the session and validate it
            var stateFile = Path.Combine(sessionPath, "state.json");
            if (!File.Exists(stateFile))
            {
                Console.WriteLine("Session state file not found.");
                return false;
            }

            Console.WriteLine("Found existing session, validating...");
            await LoadSessionAsync();
            
            // Test if session is still valid by navigating to LinkedIn Learning
            var isValid = await ValidateSessionAsync();
            
            if (!isValid)
            {
                Console.WriteLine("Session validation failed.");
                await CleanupSessionAsync();
                return false;
            }

            Console.WriteLine("✓ Session is valid!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Session validation error: {ex.Message}");
            await CleanupSessionAsync();
            return false;
        }
    }

    public async Task LoginInteractivelyAsync()
    {
        if (_browser == null)
            throw new InvalidOperationException("Browser not initialized. Call InitializeBrowserAsync() first.");

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("LINKEDIN LEARNING LOGIN REQUIRED");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("A browser window will open for you to log in to LinkedIn Learning.");
        Console.WriteLine("Please:");
        Console.WriteLine("  1. Complete the login process (email/password)");
        Console.WriteLine("  2. Complete any 2FA if prompted");
        Console.WriteLine("  3. Ensure you reach LinkedIn Learning homepage");
        Console.WriteLine("  4. Return to this console and press ENTER when done");
        Console.WriteLine(new string('=', 60));

        // Force headed mode for interactive login regardless of config
        if (_config.Headless)
        {
            Console.WriteLine("Switching to headed mode for interactive login...");
            await DisposeBrowserAsync();
            
            var headedOptions = new BrowserTypeLaunchOptions { Headless = false };
            _browser = await _playwright!.Chromium.LaunchAsync(headedOptions);
        }

        // Create new context for login
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();

        // Navigate to LinkedIn Learning login
        Console.WriteLine("Navigating to LinkedIn Learning login page...");
        await _page.GotoAsync("https://www.linkedin.com/learning/login");

        // Wait for user to complete login
        Console.WriteLine("\nPress ENTER after you have successfully logged in and can see the LinkedIn Learning homepage...");
        Console.ReadLine();

        // Verify we're on LinkedIn Learning domain
        var currentUrl = _page.Url;
        if (!currentUrl.Contains("linkedin.com"))
        {
            throw new InvalidOperationException("Please ensure you are logged in to LinkedIn Learning before continuing.");
        }

        Console.WriteLine("✓ Login completed successfully!");
    }

    public async Task SaveSessionAsync()
    {
        if (_context == null)
            throw new InvalidOperationException("No browser context to save.");

        var sessionPath = GetSessionPath();
        
        // Create session directory if it doesn't exist
        Directory.CreateDirectory(sessionPath);

        var stateFile = Path.Combine(sessionPath, "state.json");
        
        Console.WriteLine($"Saving session to: {sessionPath}");
        await _context.StorageStateAsync(new BrowserContextStorageStateOptions
        {
            Path = stateFile
        });

        Console.WriteLine("✓ Session saved successfully!");
    }

    public async Task LoadSessionAsync()
    {
        if (_browser == null)
            throw new InvalidOperationException("Browser not initialized. Call InitializeBrowserAsync() first.");

        var sessionPath = GetSessionPath();
        var stateFile = Path.Combine(sessionPath, "state.json");

        if (!File.Exists(stateFile))
            throw new FileNotFoundException($"Session state file not found: {stateFile}");

        Console.WriteLine("Loading saved session...");
        
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = stateFile
        });
        
        _page = await _context.NewPageAsync();
        Console.WriteLine("✓ Session loaded successfully!");
    }

    public async Task<bool> ValidateSessionAsync()
    {
        if (_page == null)
            return false;

        try
        {
            Console.WriteLine("Validating session by navigating to LinkedIn Learning...");
            
            // Navigate to LinkedIn Learning and check if we're logged in
            await _page.GotoAsync("https://www.linkedin.com/learning/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            // Check if we're redirected to login page or if we see LinkedIn Learning content
            var currentUrl = _page.Url;
            
            if (currentUrl.Contains("/login") || currentUrl.Contains("/uas/login"))
            {
                Console.WriteLine("Session expired - redirected to login page.");
                return false;
            }

            // Look for LinkedIn Learning specific elements to confirm we're logged in
            var navCount = await _page.Locator("nav").CountAsync();
            var isOnLearningPage = navCount > 0 || currentUrl.Contains("linkedin.com/learning");
            
            if (isOnLearningPage)
            {
                Console.WriteLine("✓ Session validation successful!");
                return true;
            }

            Console.WriteLine("Session validation failed - unexpected page content.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Session validation failed: {ex.Message}");
            return false;
        }
    }

    public async Task<Course> ProcessCourseAsync(string courseUrl)
    {
        if (_page == null)
            throw new InvalidOperationException("No active browser page. Ensure session is loaded first.");

        Console.WriteLine($"Processing course: {courseUrl}");
        
        // Navigate to course page
        await _page.GotoAsync(courseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Extract course metadata
        var course = new Course
        {
            Url = courseUrl,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            // Extract course title
            var titleElement = _page.Locator("h1").First;
            course.Title = await titleElement.TextContentAsync() ?? "Unknown Course";

            // Extract instructor name
            var instructorLocator = _page.Locator("[data-test-id='instructor-name']");
            var instructorCount = await instructorLocator.CountAsync();
            if (instructorCount > 0)
            {
                course.Instructor = await instructorLocator.First.TextContentAsync() ?? "Unknown Instructor";
            }

            Console.WriteLine($"✓ Course found: {course.Title} by {course.Instructor}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not extract all course metadata: {ex.Message}");
        }

        // TODO: Implement lesson discovery and transcript extraction
        Console.WriteLine("Course processing placeholder - lesson extraction will be implemented next.");

        return course;
    }

    private string GetSessionPath()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), _config.SessionProfile);
    }

    private Task CleanupSessionAsync()
    {
        var sessionPath = GetSessionPath();
        if (Directory.Exists(sessionPath))
        {
            try
            {
                Directory.Delete(sessionPath, true);
                Console.WriteLine("✓ Cleaned up invalid session data.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not clean up session directory: {ex.Message}");
            }
        }
        return Task.CompletedTask;
    }

    private async Task DisposeBrowserAsync()
    {
        if (_page != null)
        {
            await _page.CloseAsync();
            _page = null;
        }

        if (_context != null)
        {
            await _context.CloseAsync();
            _context = null;
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
    }

    public async Task<bool> EnsureAuthenticatedAsync()
    {
        // Initialize browser first
        await InitializeBrowserAsync();

        // Check if we have a valid session
        if (await HasValidSessionAsync())
        {
            return true; // Already authenticated
        }

        // Need to login interactively
        await LoginInteractivelyAsync();
        await SaveSessionAsync();

        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeBrowserAsync().GetAwaiter().GetResult();
            _playwright?.Dispose();
            _disposed = true;
        }
    }
}