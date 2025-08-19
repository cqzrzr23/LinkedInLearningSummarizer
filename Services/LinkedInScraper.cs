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

            // Check if it looks like a course URL (contains /learning/) (case-insensitive)
            if (!uri.AbsolutePath.ToLowerInvariant().Contains("/learning/"))
            {
                if (logMessages)
                    Console.WriteLine($"Error: URL must be a LinkedIn Learning course (/learning/). Path: {uri.AbsolutePath}");
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

    // Helper method to check if a URL is a valid lesson URL for the current course
    private bool IsValidLessonUrl(string url, string courseUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(courseUrl))
            return false;

        try
        {
            var lessonUri = new Uri(url);
            var courseUri = new Uri(courseUrl);
            
            // Must be LinkedIn Learning domain
            if (!lessonUri.Host.Contains("linkedin.com"))
                return false;
            
            // Must contain /learning/ path
            if (!lessonUri.AbsolutePath.Contains("/learning/"))
                return false;
            
            // Should not be the exact same URL as the course (ignore query parameters)
            if (lessonUri.AbsolutePath.Equals(courseUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                return false;
                
            // Filter out LinkedIn help/support URLs
            if (lessonUri.AbsolutePath.Contains("/help/") || 
                lessonUri.Host.Contains("help.linkedin.com"))
                return false;
            
            // Should contain course slug in the lesson URL (more flexible matching)
            var coursePath = courseUri.AbsolutePath.TrimEnd('/');
            var lessonPath = lessonUri.AbsolutePath;
            
            // Extract course slug from course URL (e.g., "agentic-ai-a-framework-for-planning-and-execution")
            var courseSlugMatch = System.Text.RegularExpressions.Regex.Match(coursePath, @"/learning/([^/]+)");
            if (courseSlugMatch.Success)
            {
                var courseSlug = courseSlugMatch.Groups[1].Value;
                // Lesson URL should contain the same course slug
                return lessonPath.Contains(courseSlug);
            }
            
            // Fallback: lesson URL should start with or contain the course path
            return lessonPath.StartsWith(coursePath + "/", StringComparison.OrdinalIgnoreCase) ||
                   lessonPath.StartsWith(coursePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Debug: URL validation error for {url}: {ex.Message}");
            return false;
        }
    }

    // Helper method to check if a title represents an actual lesson (not navigation)
    private bool IsValidLessonTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var invalidTitles = new[] 
        {
            "home", "my career journey", "my library", "browse", "search", 
            "learning paths", "collections", "saved", "history", "settings",
            "profile", "help", "logout", "sign out", "login", "sign in"
        };

        var cleanTitle = title.Trim().ToLowerInvariant();
        return !invalidTitles.Contains(cleanTitle);
    }

    public async Task<List<Lesson>> DiscoverLessonsAsync(string? courseUrl = null)
    {
        if (_page == null)
            throw new InvalidOperationException("No active browser page. Ensure session is loaded first.");

        Console.WriteLine("Discovering course lessons...");
        var lessons = new List<Lesson>();
        var currentUrl = courseUrl ?? _page.Url;

        try
        {
            // Updated selectors specifically targeting course content areas
            var lessonLinkSelectors = new[]
            {
                // Try course-specific table of contents first
                ".classroom-toc-chapter a[href*='/learning/']",
                ".learning-course-toc a[href*='/learning/']",
                "[data-test-id*='course-content'] a[href*='/learning/']",
                "[data-test-id='table-of-contents'] a[href*='/learning/']",
                ".course-outline a[href*='/learning/']",
                ".table-of-contents a[href*='/learning/']",
                ".course-toc a[href*='/learning/']",
                ".contents-panel a[href*='/learning/']",
                // Fallback to more general but still scoped selectors
                ".classroom-nav a[href*='/learning/']",
                "aside a[href*='/learning/']"
            };

            ILocator? lessonLinks = null;
            string usedSelector = "";
            var allCandidates = new List<(string url, string title, ILocator element)>();

            // Try each selector and collect all candidates
            foreach (var selector in lessonLinkSelectors)
            {
                lessonLinks = _page.Locator(selector);
                var count = await lessonLinks.CountAsync();
                if (count > 0)
                {
                    usedSelector = selector;
                    Console.WriteLine($"Found {count} potential lessons using selector: {selector}");
                    
                    // Collect all candidates from this selector
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var element = lessonLinks.Nth(i);
                            var url = await element.GetAttributeAsync("href");
                            var title = await element.TextContentAsync();
                            
                            if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(title))
                            {
                                // Make URL absolute
                                if (url.StartsWith("/"))
                                {
                                    url = "https://www.linkedin.com" + url;
                                }
                                
                                allCandidates.Add((url, title.Trim(), element));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Error reading candidate {i + 1}: {ex.Message}");
                        }
                    }
                    
                    // If we found valid candidates, stop trying other selectors
                    if (allCandidates.Any())
                        break;
                }
            }

            if (!allCandidates.Any())
            {
                Console.WriteLine("Warning: Could not find lesson links. Course may have different layout.");
                return lessons;
            }

            Console.WriteLine($"Filtering {allCandidates.Count} candidates...");

            // Filter and validate candidates
            var validLessons = new List<(string url, string title, int order)>();
            var seenUrls = new HashSet<string>();

            foreach (var (url, title, element) in allCandidates)
            {
                // Skip duplicates
                if (seenUrls.Contains(url))
                    continue;

                // Validate lesson URL and title
                var isValidUrl = IsValidLessonUrl(url, currentUrl);
                var isValidTitle = IsValidLessonTitle(title);
                
                if (isValidUrl && isValidTitle)
                {
                    validLessons.Add((url, title, validLessons.Count + 1));
                    seenUrls.Add(url);
                    Console.WriteLine($"  ✓ Valid lesson: {title}");
                }
                else
                {
                    var reason = !isValidUrl ? "invalid URL" : "invalid title";
                    Console.WriteLine($"  ✗ Filtered out ({reason}): {title}");
                    Console.WriteLine($"      URL: {url}");
                    Console.WriteLine($"      Course: {currentUrl}");
                }
            }

            // Convert to Lesson objects
            foreach (var (url, title, order) in validLessons)
            {
                var lesson = new Lesson
                {
                    Url = url,
                    Title = title,
                    LessonNumber = order
                };

                lessons.Add(lesson);
            }

            Console.WriteLine($"✓ Discovered {lessons.Count} valid lessons");
            if (lessons.Any())
            {
                Console.WriteLine("Sample lessons found:");
                foreach (var lesson in lessons.Take(3))
                {
                    Console.WriteLine($"  {lesson.LessonNumber}. {lesson.Title}");
                }
                if (lessons.Count > 3)
                {
                    Console.WriteLine($"  ... and {lessons.Count - 3} more lessons");
                }
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
        var lessons = await DiscoverLessonsAsync(courseUrl);
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

    // ============== TRANSCRIPT EXTRACTION METHODS ==============

    public async Task NavigateToLessonAsync(string lessonUrl)
    {
        if (string.IsNullOrWhiteSpace(lessonUrl))
            throw new ArgumentException("Lesson URL cannot be empty", nameof(lessonUrl));

        if (_page == null)
            throw new InvalidOperationException("Page not initialized. Ensure browser is initialized first.");

        Console.WriteLine($"Navigating to lesson: {lessonUrl}");

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Navigate with retry logic
                var response = await _page.GotoAsync(lessonUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30000
                });

                if (response?.Status == 200)
                {
                    // Wait for video player to be present
                    await _page.WaitForSelectorAsync("video, .classroom-workspace", new PageWaitForSelectorOptions
                    {
                        Timeout = 10000,
                        State = WaitForSelectorState.Attached
                    });

                    Console.WriteLine($"✓ Successfully navigated to lesson");
                    return;
                }
                else
                {
                    Console.WriteLine($"Navigation returned status: {response?.Status}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                        continue;
                    }
                }
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Navigation timeout (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Navigation error (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }
                throw;
            }
        }

        throw new Exception($"Failed to navigate to lesson after {maxRetries} attempts");
    }

    public async Task<bool> ClickTranscriptTabAsync()
    {
        if (_page == null)
            throw new InvalidOperationException("Page not initialized");

        Console.WriteLine("Looking for transcript tab...");

        // Try multiple selectors for the transcript tab
        var transcriptSelectors = new[]
        {
            "button:has-text('Transcript')",
            "[role='tab']:has-text('Transcript')",
            ".classroom-nav-item:has-text('Transcript')",
            "text=Transcript",
            "[aria-label*='Transcript']"
        };

        foreach (var selector in transcriptSelectors)
        {
            try
            {
                var element = await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                {
                    Timeout = 5000,
                    State = WaitForSelectorState.Visible
                });

                if (element != null)
                {
                    await element.ClickAsync();
                    Console.WriteLine($"✓ Clicked transcript tab using selector: {selector}");
                    
                    // Wait for transcript content to load
                    await Task.Delay(1000);
                    return true;
                }
            }
            catch (TimeoutException)
            {
                // Try next selector
                continue;
            }
        }

        Console.WriteLine("⚠ Transcript tab not found");
        return false;
    }

    public async Task<bool> DisableInteractiveTranscriptsAsync()
    {
        if (_page == null)
            throw new InvalidOperationException("Page not initialized");

        Console.WriteLine("Checking interactive transcript toggle...");

        try
        {
            // Look for the toggle switch near "Enable interactive transcripts" text
            // First, find the text
            var toggleText = await _page.GetByText("Enable interactive transcripts").First.IsVisibleAsync();
            
            if (!toggleText)
            {
                Console.WriteLine("Interactive transcript toggle not found");
                return false;
            }

            // Find the toggle switch - it's usually a button or input near the text
            var toggleSelectors = new[]
            {
                "text=Enable interactive transcripts >> .. >> button",
                "text=Enable interactive transcripts >> .. >> [role='switch']",
                "text=Enable interactive transcripts >> .. >> input[type='checkbox']",
                "[aria-label*='interactive transcript']",
                ".transcript-settings button, .transcript-settings [role='switch']"
            };

            foreach (var selector in toggleSelectors)
            {
                try
                {
                    var toggle = await _page.QuerySelectorAsync(selector);
                    if (toggle != null)
                    {
                        // Check if toggle is ON (usually aria-checked="true" or similar)
                        var isChecked = await toggle.GetAttributeAsync("aria-checked");
                        var isPressed = await toggle.GetAttributeAsync("aria-pressed");
                        var dataChecked = await toggle.GetAttributeAsync("data-checked");

                        bool isOn = isChecked == "true" || isPressed == "true" || dataChecked == "true";

                        if (isOn)
                        {
                            Console.WriteLine("Interactive transcripts is ON, turning it OFF...");
                            await toggle.ClickAsync();
                            await Task.Delay(1000); // Wait for UI to update
                            Console.WriteLine("✓ Disabled interactive transcripts");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine("✓ Interactive transcripts already disabled");
                            return true;
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Alternative approach: Click on the text itself if it's clickable
            var textElement = await _page.GetByText("Enable interactive transcripts").First.ElementHandleAsync();
            if (textElement != null)
            {
                await textElement.ClickAsync();
                await Task.Delay(1000);
                Console.WriteLine("✓ Toggled interactive transcripts by clicking text");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling interactive transcript toggle: {ex.Message}");
        }

        return false;
    }

    public async Task<string> ExtractTranscriptTextAsync()
    {
        if (_page == null)
            throw new InvalidOperationException("Page not initialized");

        Console.WriteLine("Extracting transcript text...");

        try
        {
            // Wait for transcript content to be present
            await _page.WaitForSelectorAsync(".classroom-transcript__lines", new PageWaitForSelectorOptions
            {
                Timeout = 10000,
                State = WaitForSelectorState.Visible
            });

            // With interactive transcripts disabled, the entire transcript should be in a single <p> element
            var transcriptText = await _page.EvaluateAsync<string>(@"
                () => {
                    const container = document.querySelector('.classroom-transcript__lines');
                    if (!container) return '';
                    
                    // Look for the single <p> element containing all text
                    const paragraph = container.querySelector('p');
                    if (paragraph) {
                        return paragraph.innerText || paragraph.textContent || '';
                    }
                    
                    // Fallback: get all text from container
                    return container.innerText || container.textContent || '';
                }
            ");

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                Console.WriteLine("⚠ No transcript text found");
                return string.Empty;
            }

            // Clean the extracted text
            transcriptText = CleanTranscriptText(transcriptText);
            
            Console.WriteLine($"✓ Extracted transcript ({transcriptText.Length} characters)");
            return transcriptText;
        }
        catch (TimeoutException)
        {
            Console.WriteLine("⚠ Transcript content not found");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting transcript: {ex.Message}");
            return string.Empty;
        }
    }

    private string CleanTranscriptText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remove excessive whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        
        // Trim start and end
        text = text.Trim();
        
        // Remove any potential HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);

        return text;
    }

    // Temporary method for testing - will be replaced by MarkdownGenerator in Week 6
    private async Task SaveTranscriptForTesting(Lesson lesson, string transcriptText)
    {
        if (string.IsNullOrWhiteSpace(transcriptText)) return;
        
        try
        {
            var outputDir = Path.Combine(_config.OutputTranscriptDir, "test-extraction");
            Directory.CreateDirectory(outputDir);
            
            // Simple filename sanitization
            var safeTitle = System.Text.RegularExpressions.Regex.Replace(lesson.Title, @"[^\w\s-]", "").Trim();
            safeTitle = System.Text.RegularExpressions.Regex.Replace(safeTitle, @"\s+", "-");
            if (safeTitle.Length > 50) safeTitle = safeTitle.Substring(0, 50);
            
            var filename = $"lesson-{lesson.LessonNumber:D2}-{safeTitle}.txt";
            var filepath = Path.Combine(outputDir, filename);
            
            // Write transcript with metadata header
            var content = $"Lesson {lesson.LessonNumber}: {lesson.Title}\n";
            content += $"URL: {lesson.Url}\n";
            content += $"Duration: {lesson.Duration}\n";
            content += $"Extracted: {lesson.ExtractedAt:yyyy-MM-dd HH:mm:ss} UTC\n";
            content += $"{new string('=', 80)}\n\n";
            content += transcriptText;
            
            await File.WriteAllTextAsync(filepath, content);
            Console.WriteLine($"  → Saved test output to: {filename}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Failed to save test file: {ex.Message}");
        }
    }

    public async Task<string> ExtractLessonTranscriptAsync(Lesson lesson)
    {
        if (lesson == null)
            throw new ArgumentNullException(nameof(lesson));

        Console.WriteLine($"\n--- Extracting transcript for: {lesson.Title} ---");

        const int maxRetries = 3;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Step 1: Navigate to the lesson
                await NavigateToLessonAsync(lesson.Url);

                // Step 2: Click the transcript tab
                bool transcriptTabClicked = await ClickTranscriptTabAsync();
                if (!transcriptTabClicked)
                {
                    lesson.HasTranscript = false;
                    lesson.Transcript = string.Empty;
                    Console.WriteLine("No transcript available for this lesson");
                    return string.Empty;
                }

                // Step 3: Disable interactive transcripts for simpler extraction
                await DisableInteractiveTranscriptsAsync();

                // Step 4: Extract the transcript text
                string transcriptText = await ExtractTranscriptTextAsync();

                // Update the lesson object
                lesson.Transcript = transcriptText;
                lesson.HasTranscript = !string.IsNullOrWhiteSpace(transcriptText);
                lesson.ExtractedAt = DateTime.UtcNow;

                if (lesson.HasTranscript)
                {
                    Console.WriteLine($"✓ Successfully extracted transcript for lesson {lesson.LessonNumber}");
                    
                    // Temporary: Save transcript for testing (will be removed in Week 6)
                    await SaveTranscriptForTesting(lesson, transcriptText);
                }
                else
                {
                    Console.WriteLine($"⚠ No transcript content for lesson {lesson.LessonNumber}");
                }

                return transcriptText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Attempt {attempt}/{maxRetries} failed: {ex.Message}");
                
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }
                
                // Final attempt failed
                lesson.HasTranscript = false;
                lesson.Transcript = string.Empty;
                lesson.ExtractedAt = DateTime.UtcNow;
                
                Console.WriteLine($"✗ Failed to extract transcript for lesson {lesson.LessonNumber} after {maxRetries} attempts");
                throw;
            }
        }

        return string.Empty;
    }

    public async Task ProcessLessonTranscriptsAsync(List<Lesson> lessons)
    {
        if (lessons == null || !lessons.Any())
        {
            Console.WriteLine("No lessons to process");
            return;
        }

        Console.WriteLine($"\n========================================");
        Console.WriteLine($"Processing transcripts for {lessons.Count} lessons");
        Console.WriteLine($"========================================\n");

        int successCount = 0;
        int failureCount = 0;
        var failedLessons = new List<string>();

        for (int i = 0; i < lessons.Count; i++)
        {
            var lesson = lessons[i];
            Console.WriteLine($"\nProcessing lesson {i + 1}/{lessons.Count}: {lesson.Title}");

            try
            {
                await ExtractLessonTranscriptAsync(lesson);
                
                if (lesson.HasTranscript)
                {
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"Lesson {lesson.LessonNumber} has no transcript available");
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                failedLessons.Add($"Lesson {lesson.LessonNumber}: {lesson.Title}");
                Console.WriteLine($"Failed to process lesson {lesson.LessonNumber}: {ex.Message}");
            }

            // Add a small delay between lessons to avoid rate limiting
            if (i < lessons.Count - 1)
            {
                await Task.Delay(2000);
            }
        }

        // Summary report
        Console.WriteLine($"\n========================================");
        Console.WriteLine($"TRANSCRIPT EXTRACTION SUMMARY");
        Console.WriteLine($"========================================");
        Console.WriteLine($"Total Lessons: {lessons.Count}");
        Console.WriteLine($"Successful: {successCount}");
        Console.WriteLine($"No Transcript: {lessons.Count - successCount - failureCount}");
        Console.WriteLine($"Failed: {failureCount}");
        
        if (failedLessons.Any())
        {
            Console.WriteLine($"\nFailed Lessons:");
            foreach (var failed in failedLessons)
            {
                Console.WriteLine($"  - {failed}");
            }
        }

        Console.WriteLine($"========================================\n");
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