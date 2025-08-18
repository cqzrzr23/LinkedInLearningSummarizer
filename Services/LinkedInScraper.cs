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

    public bool ValidateCourseUrl(string url, bool logMessages = true)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            if (logMessages)
                Console.WriteLine("Error: Course URL cannot be empty.");
            return false;
        }

        try
        {
            // Handle URLs that start with // by prepending https:
            if (url.StartsWith("//"))
            {
                url = "https:" + url;
            }
            
            var uri = new Uri(url);
            
            // Check protocol - only http and https are valid
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                if (logMessages)
                    Console.WriteLine($"Error: URL must use HTTP or HTTPS protocol. Got: {uri.Scheme}");
                return false;
            }
            
            // Check if it's a LinkedIn Learning URL (case-insensitive)
            if (!uri.Host.ToLowerInvariant().Contains("linkedin.com"))
            {
                if (logMessages)
                    Console.WriteLine($"Error: URL must be from linkedin.com domain. Got: {uri.Host}");
                return false;
            }

            // Check if it's a learning URL (case-insensitive)
            if (!uri.AbsolutePath.ToLowerInvariant().Contains("/learning/"))
            {
                if (logMessages)
                    Console.WriteLine($"Error: URL must be a LinkedIn Learning course. Path: {uri.AbsolutePath}");
                return false;
            }

            // Check if it looks like a course URL (contains /courses/) (case-insensitive)
            if (!uri.AbsolutePath.ToLowerInvariant().Contains("/courses/"))
            {
                if (logMessages)
                    Console.WriteLine($"Error: URL must be a LinkedIn Learning course (/courses/). Path: {uri.AbsolutePath}");
                return false;
            }

            if (logMessages)
                Console.WriteLine($"✓ Valid LinkedIn Learning course URL: {url}");
            return true;
        }
        catch (UriFormatException ex)
        {
            if (logMessages)
                Console.WriteLine($"Error: Invalid URL format: {ex.Message}");
            return false;
        }
    }

    public async Task NavigateToCourseAsync(string courseUrl)
    {
        if (_page == null)
            throw new InvalidOperationException("No active browser page. Ensure session is loaded first.");

        if (!ValidateCourseUrl(courseUrl))
        {
            throw new ArgumentException("Invalid course URL format.", nameof(courseUrl));
        }

        Console.WriteLine($"Navigating to course: {courseUrl}");

        try
        {
            // Navigate to course page with retry logic
            var maxRetries = 3;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    await _page.GotoAsync(courseUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = 30000
                    });

                    // Check if we're redirected to login (session expired)
                    var currentUrl = _page.Url;
                    if (currentUrl.Contains("/login") || currentUrl.Contains("/uas/login"))
                    {
                        throw new InvalidOperationException("Session expired during navigation. Please re-authenticate.");
                    }

                    // Wait for course page to load completely
                    await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                    
                    Console.WriteLine($"✓ Successfully navigated to course page");
                    return;
                }
                catch (TimeoutException) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    Console.WriteLine($"Navigation timeout, retrying... (attempt {retryCount}/{maxRetries})");
                    await Task.Delay(2000); // Wait 2 seconds before retry
                }
            }

            throw new InvalidOperationException($"Failed to navigate to course after {maxRetries} attempts.");
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Navigation failed: {ex.Message}", ex);
        }
    }

    public async Task<Course> ExtractCourseMetadataAsync(string courseUrl)
    {
        if (_page == null)
            throw new InvalidOperationException("No active browser page. Ensure session is loaded first.");

        Console.WriteLine("Extracting course metadata...");

        var course = new Course
        {
            Url = courseUrl,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            // Extract course title - try multiple selectors
            var titleSelectors = new[]
            {
                "h1[data-test-id='course-title']",
                "h1.course-title",
                "h1",
                "[data-test-id='course-title']"
            };

            foreach (var selector in titleSelectors)
            {
                var titleElement = _page.Locator(selector).First;
                var titleCount = await titleElement.CountAsync();
                if (titleCount > 0)
                {
                    var titleText = await titleElement.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(titleText))
                    {
                        course.Title = titleText.Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(course.Title))
            {
                course.Title = "Unknown Course";
                Console.WriteLine("Warning: Could not extract course title");
            }

            // Extract instructor name - try multiple selectors
            var instructorSelectors = new[]
            {
                "[data-test-id='instructor-name']",
                ".course-instructor",
                ".instructor-name",
                "a[href*='/in/']"
            };

            foreach (var selector in instructorSelectors)
            {
                var instructorElement = _page.Locator(selector).First;
                var instructorCount = await instructorElement.CountAsync();
                if (instructorCount > 0)
                {
                    var instructorText = await instructorElement.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(instructorText))
                    {
                        course.Instructor = instructorText.Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(course.Instructor))
            {
                course.Instructor = "Unknown Instructor";
                Console.WriteLine("Warning: Could not extract instructor name");
            }

            // Extract course description
            var descriptionSelectors = new[]
            {
                "[data-test-id='course-description']",
                ".course-description",
                ".description",
                "meta[name='description']"
            };

            foreach (var selector in descriptionSelectors)
            {
                var descElement = _page.Locator(selector).First;
                var descCount = await descElement.CountAsync();
                if (descCount > 0)
                {
                    var descText = selector.Contains("meta") 
                        ? await descElement.GetAttributeAsync("content")
                        : await descElement.TextContentAsync();
                    
                    if (!string.IsNullOrWhiteSpace(descText))
                    {
                        course.Description = descText.Trim();
                        break;
                    }
                }
            }

            // Try to extract total lesson count from table of contents
            var tocSelectors = new[]
            {
                "[data-test-id='table-of-contents'] li",
                ".table-of-contents li",
                ".course-toc li",
                "nav li",
                "[role='list'] li"
            };

            foreach (var selector in tocSelectors)
            {
                var lessonElements = _page.Locator(selector);
                var lessonCount = await lessonElements.CountAsync();
                if (lessonCount > 0)
                {
                    course.TotalLessons = lessonCount;
                    break;
                }
            }

            Console.WriteLine($"✓ Course metadata extracted:");
            Console.WriteLine($"  Title: {course.Title}");
            Console.WriteLine($"  Instructor: {course.Instructor}");
            Console.WriteLine($"  Total Lessons: {course.TotalLessons}");
            if (!string.IsNullOrEmpty(course.Description))
            {
                var shortDesc = course.Description.Length > 100 
                    ? course.Description.Substring(0, 100) + "..."
                    : course.Description;
                Console.WriteLine($"  Description: {shortDesc}");
            }

            return course;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error extracting course metadata: {ex.Message}");
            
            // Return course with minimal info if extraction fails
            if (string.IsNullOrEmpty(course.Title))
                course.Title = "Unknown Course";
            if (string.IsNullOrEmpty(course.Instructor))
                course.Instructor = "Unknown Instructor";
                
            return course;
        }
    }

    public async Task<List<Lesson>> DiscoverLessonsAsync()
    {
        if (_page == null)
            throw new InvalidOperationException("No active browser page. Ensure session is loaded first.");

        Console.WriteLine("Discovering course lessons...");
        var lessons = new List<Lesson>();

        try
        {
            // Try multiple strategies to find lesson links
            var lessonLinkSelectors = new[]
            {
                "[data-test-id='table-of-contents'] a[href*='/learning/']",
                ".table-of-contents a[href*='/learning/']", 
                ".course-toc a[href*='/learning/']",
                "nav a[href*='/learning/']",
                "li a[href*='/learning/']"
            };

            ILocator? lessonLinks = null;
            string usedSelector = "";

            foreach (var selector in lessonLinkSelectors)
            {
                lessonLinks = _page.Locator(selector);
                var count = await lessonLinks.CountAsync();
                if (count > 0)
                {
                    usedSelector = selector;
                    Console.WriteLine($"Found {count} lessons using selector: {selector}");
                    break;
                }
            }

            if (lessonLinks == null || await lessonLinks.CountAsync() == 0)
            {
                Console.WriteLine("Warning: Could not find lesson links. Course may have different layout.");
                return lessons;
            }

            // Extract lesson information
            var lessonCount = await lessonLinks.CountAsync();
            for (int i = 0; i < lessonCount; i++)
            {
                var lessonLink = lessonLinks.Nth(i);
                
                try
                {
                    var url = await lessonLink.GetAttributeAsync("href");
                    var title = await lessonLink.TextContentAsync();
                    
                    if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(title))
                    {
                        // Ensure URL is absolute
                        if (url.StartsWith("/"))
                        {
                            url = "https://www.linkedin.com" + url;
                        }

                        var lesson = new Lesson
                        {
                            Url = url,
                            Title = title.Trim(),
                            LessonNumber = i + 1
                        };

                        lessons.Add(lesson);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error extracting lesson {i + 1}: {ex.Message}");
                }
            }

            Console.WriteLine($"✓ Discovered {lessons.Count} lessons");
            foreach (var lesson in lessons.Take(3))
            {
                Console.WriteLine($"  {lesson.LessonNumber}. {lesson.Title}");
            }
            if (lessons.Count > 3)
            {
                Console.WriteLine($"  ... and {lessons.Count - 3} more lessons");
            }

            return lessons;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during lesson discovery: {ex.Message}");
            return lessons;
        }
    }

    public async Task<Course> ProcessCourseAsync(string courseUrl)
    {
        if (_page == null)
            throw new InvalidOperationException("No active browser page. Ensure session is loaded first.");

        Console.WriteLine($"Processing course: {courseUrl}");
        
        // Navigate to course page
        await NavigateToCourseAsync(courseUrl);

        // Extract course metadata
        var course = await ExtractCourseMetadataAsync(courseUrl);

        // Discover lessons
        var lessons = await DiscoverLessonsAsync();
        course.Lessons = lessons;
        course.TotalLessons = lessons.Count;

        Console.WriteLine($"✓ Course processing completed: {course.Title} ({course.TotalLessons} lessons)");

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