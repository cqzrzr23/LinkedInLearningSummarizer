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

    private void LogDebug(string message)
    {
        // Write to console only if available (not in test environments)
        try
        {
            if (Console.Out != null && Console.Out != System.IO.TextWriter.Null)
            {
                Console.WriteLine(message);
            }
        }
        catch
        {
            // Silently fail if console is not available
        }
    }

    public async Task InitializeBrowserAsync()
    {
        if (_playwright != null)
            return; // Already initialized

        LogDebug("Initializing browser...");
        
        _playwright = await Playwright.CreateAsync();
        
        var browserOptions = new BrowserTypeLaunchOptions
        {
            Headless = _config.Headless
        };

        // Use Chromium for better LinkedIn compatibility
        _browser = await _playwright.Chromium.LaunchAsync(browserOptions);
        
        LogDebug($"✓ Browser initialized (Headless: {_config.Headless})");
    }

    public async Task<bool> HasValidSessionAsync()
    {
        var sessionPath = GetSessionPath();
        
        if (!Directory.Exists(sessionPath))
        {
            LogDebug("No existing session found.");
            return false;
        }

        try
        {
            // Try to load the session and validate it
            var stateFile = Path.Combine(sessionPath, "state.json");
            if (!File.Exists(stateFile))
            {
                LogDebug("Session state file not found.");
                return false;
            }

            LogDebug("Found existing session, validating...");
            await LoadSessionAsync();
            
            // Test if session is still valid by navigating to LinkedIn Learning
            var isValid = await ValidateSessionAsync();
            
            if (!isValid)
            {
                LogDebug("Session validation failed.");
                await CleanupSessionAsync();
                return false;
            }

            LogDebug("✓ Session is valid!");
            return true;
        }
        catch (Exception ex)
        {
            LogDebug($"Session validation error: {ex.Message}");
            await CleanupSessionAsync();
            return false;
        }
    }

    public async Task LoginInteractivelyAsync()
    {
        if (_browser == null)
            throw new InvalidOperationException("Browser not initialized. Call InitializeBrowserAsync() first.");

        LogDebug("\n" + new string('=', 60));
        LogDebug("LINKEDIN LEARNING LOGIN REQUIRED");
        LogDebug(new string('=', 60));
        LogDebug("A browser window will open for you to log in to LinkedIn Learning.");
        LogDebug("Please:");
        LogDebug("  1. Complete the login process (email/password)");
        LogDebug("  2. Complete any 2FA if prompted");
        LogDebug("  3. Ensure you reach LinkedIn Learning homepage");
        LogDebug("  4. Return to this console and press ENTER when done");
        LogDebug(new string('=', 60));

        // Force headed mode for interactive login regardless of config
        if (_config.Headless)
        {
            LogDebug("Switching to headed mode for interactive login...");
            await DisposeBrowserAsync();
            
            var headedOptions = new BrowserTypeLaunchOptions { Headless = false };
            _browser = await _playwright!.Chromium.LaunchAsync(headedOptions);
        }

        // Create new context for login
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();

        // Navigate to LinkedIn Learning login
        LogDebug("Navigating to LinkedIn Learning login page...");
        await _page.GotoAsync("https://www.linkedin.com/learning/login");

        // Wait for user to complete login
        LogDebug("\nPress ENTER after you have successfully logged in and can see the LinkedIn Learning homepage...");
        Console.ReadLine();

        // Verify we're on LinkedIn Learning domain
        var currentUrl = _page.Url;
        if (!currentUrl.Contains("linkedin.com"))
        {
            throw new InvalidOperationException("Please ensure you are logged in to LinkedIn Learning before continuing.");
        }

        LogDebug("✓ Login completed successfully!");
    }

    public async Task SaveSessionAsync()
    {
        if (_context == null)
            throw new InvalidOperationException("No browser context to save.");

        var sessionPath = GetSessionPath();
        
        // Create session directory if it doesn't exist
        Directory.CreateDirectory(sessionPath);

        var stateFile = Path.Combine(sessionPath, "state.json");
        
        LogDebug($"Saving session to: {sessionPath}");
        await _context.StorageStateAsync(new BrowserContextStorageStateOptions
        {
            Path = stateFile
        });

        LogDebug("✓ Session saved successfully!");
    }

    public async Task LoadSessionAsync()
    {
        if (_browser == null)
            throw new InvalidOperationException("Browser not initialized. Call InitializeBrowserAsync() first.");

        var sessionPath = GetSessionPath();
        var stateFile = Path.Combine(sessionPath, "state.json");

        if (!File.Exists(stateFile))
            throw new FileNotFoundException($"Session state file not found: {stateFile}");

        LogDebug("Loading saved session...");
        
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = stateFile
        });
        
        _page = await _context.NewPageAsync();
        LogDebug("✓ Session loaded successfully!");
    }

    public async Task<bool> ValidateSessionAsync()
    {
        if (_page == null)
            return false;

        try
        {
            LogDebug("Validating session by navigating to LinkedIn Learning...");
            
            // Navigate to LinkedIn Learning and check if we're logged in
            await _page.GotoAsync("https://www.linkedin.com/learning/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 45000  // Increased from 30s for better reliability
            });

            // Check if we're redirected to login page or if we see LinkedIn Learning content
            var currentUrl = _page.Url;
            
            if (currentUrl.Contains("/login") || currentUrl.Contains("/uas/login"))
            {
                LogDebug("Session expired - redirected to login page.");
                return false;
            }

            // Look for LinkedIn Learning specific elements to confirm we're logged in
            var navCount = await _page.Locator("nav").CountAsync();
            var isOnLearningPage = navCount > 0 || currentUrl.Contains("linkedin.com/learning");
            
            if (isOnLearningPage)
            {
                LogDebug("✓ Session validation successful!");
                return true;
            }

            LogDebug("Session validation failed - unexpected page content.");
            return false;
        }
        catch (Exception ex)
        {
            LogDebug($"Session validation failed: {ex.Message}");
            return false;
        }
    }

    public bool ValidateCourseUrl(string url, bool logMessages = true)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            if (logMessages)
                LogDebug("Error: Course URL cannot be empty.");
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
                    LogDebug($"Error: URL must use HTTP or HTTPS protocol. Got: {uri.Scheme}");
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
            var pathLower = uri.AbsolutePath.ToLowerInvariant();
            if (!pathLower.Contains("/learning/"))
            {
                if (logMessages)
                    Console.WriteLine($"Error: URL must be a LinkedIn Learning course. Path: {uri.AbsolutePath}");
                return false;
            }

            // Check if it's actually a course URL, not other LinkedIn Learning pages
            var invalidPaths = new[] {
                "/learning/topics/",
                "/learning/paths/",
                "/learning/subscription",
                "/learning/browse",
                "/learning/collections/",
                "/learning/instructors/",
                "/learning/me/"
            };

            // Check for exact matches or if path ends at just "/learning/"
            if (pathLower == "/learning/" || pathLower == "/learning" || 
                invalidPaths.Any(invalid => pathLower.Contains(invalid)))
            {
                if (logMessages)
                    Console.WriteLine($"Error: URL must be a specific course, not a general LinkedIn Learning page. Path: {uri.AbsolutePath}");
                return false;
            }

            // Ensure it has the pattern /learning/[course-name] (at least one character after /learning/)
            var learningIndex = pathLower.IndexOf("/learning/");
            if (learningIndex >= 0)
            {
                var afterLearning = pathLower.Substring(learningIndex + "/learning/".Length);
                // Remove any trailing slashes and check if there's actual content
                afterLearning = afterLearning.TrimEnd('/');
                
                if (string.IsNullOrEmpty(afterLearning))
                {
                    if (logMessages)
                        Console.WriteLine($"Error: URL must specify a course name after /learning/. Path: {uri.AbsolutePath}");
                    return false;
                }
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
                        Timeout = 45000  // Increased from 30s for better reliability
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
                    Console.WriteLine($"Navigation timeout, retrying... (nav attempt {retryCount}/{maxRetries})");
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
            LogDebug("🔍 DEBUG: Extracting course title...");
            var titleSelectors = new[]
            {
                "h1.classroom-nav__title",                    // Current LinkedIn Learning structure
                "h1.classroom-nav__title.clamp-1",           // More specific match
                "h1[data-test-id='course-title']",           // Legacy selector
                "h1.course-title",                           // Legacy selector  
                "h1",                                        // Fallback (may grab wrong element)
                "[data-test-id='course-title']"              // Legacy fallback
            };

            foreach (var selector in titleSelectors)
            {
                LogDebug($"  → Trying selector: {selector}");
                var titleElement = _page.Locator(selector).First;
                var titleCount = await titleElement.CountAsync();
                LogDebug($"    Elements found: {titleCount}");
                
                if (titleCount > 0)
                {
                    var titleText = await titleElement.TextContentAsync();
                    LogDebug($"    Text content: '{titleText}'");
                    
                    if (!string.IsNullOrWhiteSpace(titleText))
                    {
                        course.Title = titleText.Trim();
                        LogDebug($"  ✓ Course title extracted: '{course.Title}'");
                        break;
                    }
                    else
                    {
                        LogDebug("    Text content is empty or whitespace");
                    }
                }
            }

            if (string.IsNullOrEmpty(course.Title))
            {
                course.Title = "Unknown Course";
                LogDebug("⚠ Warning: Could not extract course title, using fallback");
            }
            else
            {
                LogDebug($"✓ Final course title: '{course.Title}'");
            }

            // Extract instructor name - try multiple selectors with better targeting
            var instructorSelectors = new[]
            {
                // LinkedIn Learning specific selectors (based on actual HTML structure)
                ".instructor__name",                              // Primary: exact class containing instructor name
                ".instructor__info .instructor__name",           // More specific: within instructor info container
                "a.instructor__link .instructor__name",          // Within instructor link container
                ".instructor__info",                             // Fallback: entire instructor info container
                
                // Generic selectors for other course layouts
                "[data-test-id='instructor-name']",
                ".course-instructor",
                ".instructor-name", 
                ".classroom-instructor-name",
                ".course-header .instructor",
                "h2 + p",  // Often instructor name follows course title
                "a[href*='/in/'][title]",  // LinkedIn profile links with title attribute
                "a[href*='/in/']"
            };

            LogDebug("  → Attempting to extract instructor name...");
            foreach (var selector in instructorSelectors)
            {
                try
                {
                    var instructorElement = _page.Locator(selector).First;
                    var instructorCount = await instructorElement.CountAsync();
                    LogDebug($"    Testing '{selector}' → {instructorCount} elements found");
                    
                    if (instructorCount > 0)
                    {
                        // Try title attribute first (for profile links)
                        var titleAttr = await instructorElement.GetAttributeAsync("title");
                        if (!string.IsNullOrWhiteSpace(titleAttr) && !titleAttr.Contains("LinkedIn Profile"))
                        {
                            course.Instructor = CleanInstructorName(titleAttr.Trim());
                            LogDebug($"  ✓ Instructor from title attribute via '{selector}': '{course.Instructor}'");
                            break;
                        }
                        
                        // Fall back to text content
                        var instructorText = await instructorElement.TextContentAsync();
                        LogDebug($"    Raw text content: '{(instructorText?.Length > 100 ? instructorText.Substring(0, 100) + "..." : instructorText)}'");
                        
                        if (!string.IsNullOrWhiteSpace(instructorText))
                        {
                            var cleanInstructor = CleanInstructorName(instructorText.Trim());
                            if (!string.IsNullOrWhiteSpace(cleanInstructor))
                            {
                                course.Instructor = cleanInstructor;
                                LogDebug($"  ✓ Instructor from text content via '{selector}': '{course.Instructor}'");
                                break;
                            }
                            else
                            {
                                LogDebug($"    Cleaned text was empty after processing");
                            }
                        }
                        else
                        {
                            LogDebug($"    Text content was null or empty");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"  → Warning: Instructor selector '{selector}' failed: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(course.Instructor))
            {
                course.Instructor = "Unknown Instructor";
                LogDebug("⚠ Warning: Could not extract instructor name, using fallback");
            }
            else
            {
                LogDebug($"✓ Final instructor name: '{course.Instructor}'");
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

    // Helper method to clean lesson titles by removing status indicators and duration text
    private string CleanLessonTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        // Remove common status indicators and duration patterns
        var patterns = new[]
        {
            @"\(In progress\)",          // (In progress)
            @"\(Viewed\)",               // (Viewed) 
            @"\(Not started\)",          // (Not started)
            @"\(\s*\d+m\s+\d+s\s+video\s*\)", // (3m 4s video)
            @"\d+m\s+\d+s\s+video",      // 3m 4s video
            @"\d+h\s+\d+m\s+video",      // 1h 5m video
            @"\d+s\s+video",             // 45s video
            @"\d+m\s+video",             // 5m video
            @"\d+h\s+video"              // 2h video
        };

        var cleaned = title;
        foreach (var pattern in patterns)
        {
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, pattern, "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Clean up extra whitespace and normalize
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim();

        return cleaned;
    }

    // Helper method to clean instructor name by removing link text
    private string CleanInstructorName(string instructor)
    {
        if (string.IsNullOrWhiteSpace(instructor))
            return instructor;

        // Remove common LinkedIn link text patterns
        var patterns = new[]
        {
            @"Go to LinkedIn Profile",
            @"View Profile",
            @"LinkedIn Profile",
            @"Profile"
        };

        var cleaned = instructor;
        foreach (var pattern in patterns)
        {
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, pattern, "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Clean up extra whitespace
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim();

        return cleaned;
    }

    public async Task<List<Lesson>> DiscoverLessonsAsync(string? courseUrl = null)
    {
        if (_page == null)
            throw new InvalidOperationException("No active browser page. Ensure session is loaded first.");

        LogDebug("🔍 DEBUG: Discovering course lessons...");
        LogDebug($"  Course URL: {courseUrl ?? _page.Url}");
        var lessons = new List<Lesson>();
        var currentUrl = courseUrl ?? _page.Url;

        try
        {
            // Wait for the course table of contents to load
            LogDebug("  → Waiting for course TOC to load...");
            try
            {
                // Wait for the main TOC container to be visible
                await _page.Locator(".classroom-layout-sidebar-body").WaitForAsync(
                    new LocatorWaitForOptions { Timeout = 10000 });
                
                // Additional wait for dynamic content
                await Task.Delay(2000);
                LogDebug("  → Course TOC loaded successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"  → Warning: TOC wait timeout ({ex.Message}), proceeding anyway");
            }
            // Updated selectors based on actual LinkedIn Learning structure (from lessons.txt analysis)
            var lessonLinkSelectors = new[]
            {
                // Primary selectors (based on actual LinkedIn Learning HTML structure)
                "a.classroom-toc-item__link[href*='/learning/']",           // Direct lesson links (MAIN SELECTOR)
                ".classroom-toc-section a[href*='/learning/']",            // Lesson links in TOC sections
                ".classroom-layout-sidebar-body a[href*='/learning/']",    // Links in sidebar body
                ".classroom-toc-item a[href*='/learning/']",               // Links in TOC items
                
                // Secondary selectors (more specific containers)
                ".classroom-toc-section__items a[href*='/learning/']",     // Items within TOC sections
                "li.classroom-toc-item a[href*='/learning/']",             // List items with lesson links
                
                // Legacy/backup selectors (keep for compatibility)
                ".classroom-toc-chapter a[href*='/learning/']",            // Legacy classroom TOC
                ".learning-course-toc a[href*='/learning/']",              // Legacy course TOC
                "[data-test-id*='course-content'] a[href*='/learning/']",  // Test IDs
                "[data-test-id='table-of-contents'] a[href*='/learning/']",
                ".table-of-contents a[href*='/learning/']",                // Generic TOC
                ".contents-panel a[href*='/learning/']",                   // Contents panel
                
                // Navigation selectors (lower priority)
                ".classroom-nav a[href*='/learning/']",                    // Classroom nav
                
                // Fallbacks (least reliable - will catch site navigation)
                "nav a[href*='/learning/']",                               // Generic navigation (may catch site nav)
                "main a[href*='/learning/']",                              // Main content area
                "aside a[href*='/learning/']"                              // Sidebar content (least reliable)
            };

            ILocator? lessonLinks = null;
            string usedSelector = "";
            var allCandidates = new List<(string url, string title, ILocator element)>();

            // Try each selector and collect all candidates
            LogDebug("  → Testing selectors for lesson links:");
            foreach (var selector in lessonLinkSelectors)
            {
                lessonLinks = _page.Locator(selector);
                var count = await lessonLinks.CountAsync();
                LogDebug($"    '{selector}' → {count} elements found");
                
                if (count > 0)
                {
                    usedSelector = selector;
                    LogDebug($"  ✓ Using selector: {selector} ({count} potential lessons)");
                    
                    // Collect all candidates from this selector
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var element = lessonLinks.Nth(i);
                            var url = await element.GetAttributeAsync("href");
                            var rawTitle = await element.TextContentAsync();
                            
                            if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(rawTitle))
                            {
                                // Make URL absolute
                                if (url.StartsWith("/"))
                                {
                                    url = "https://www.linkedin.com" + url;
                                }
                                
                                // Clean the title to remove status indicators and duration text
                                var cleanTitle = CleanLessonTitle(rawTitle.Trim());
                                allCandidates.Add((url, cleanTitle, element));
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

            LogDebug($"  → Filtering {allCandidates.Count} candidates...");

            // Debug: Show all candidates first
            LogDebug($"  → All candidates found:");
            for (int i = 0; i < allCandidates.Count; i++)
            {
                var (url, title, element) = allCandidates[i];
                LogDebug($"    {i+1}. '{title}' → {url}");
            }

            // Filter and validate candidates
            var validLessons = new List<(string url, string title, int order)>();
            var seenUrls = new HashSet<string>();

            LogDebug($"  → Validation results:");
            foreach (var (url, title, element) in allCandidates)
            {
                // Skip duplicates
                if (seenUrls.Contains(url))
                {
                    LogDebug($"    ❌ DUPLICATE: '{title}'");
                    continue;
                }

                // Validate lesson URL and title
                var isValidUrl = IsValidLessonUrl(url, currentUrl);
                var isValidTitle = IsValidLessonTitle(title);
                
                if (isValidUrl && isValidTitle)
                {
                    validLessons.Add((url, title, validLessons.Count + 1));
                    seenUrls.Add(url);
                    LogDebug($"    ✅ VALID: '{title}'");
                }
                else
                {
                    var reason = !isValidUrl ? "invalid URL" : "invalid title";
                    LogDebug($"    ❌ {reason.ToUpper()}: '{title}'");
                    LogDebug($"       URL: {url}");
                    LogDebug($"       Course: {currentUrl}");
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
        
        // Ensure we navigate to the main course page (not a specific lesson)
        var mainCourseUrl = ExtractMainCourseUrl(courseUrl);
        LogDebug($"🔍 DEBUG: Main course URL: {mainCourseUrl}");
        
        // Navigate to course page
        await NavigateToCourseAsync(mainCourseUrl);

        // Extract course metadata
        var course = await ExtractCourseMetadataAsync(courseUrl);

        // Discover lessons
        var lessons = await DiscoverLessonsAsync(courseUrl);
        course.Lessons = lessons;
        course.TotalLessons = lessons.Count;

        Console.WriteLine($"✓ Course processing completed: {course.Title} ({course.TotalLessons} lessons)");

        return course;
    }

    /// <summary>
    /// Extracts the main course URL from a lesson-specific URL
    /// </summary>
    private string ExtractMainCourseUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // LinkedIn Learning URLs: /learning/course-name or /learning/course-name/lesson-name
            if (pathParts.Length >= 2 && pathParts[0] == "learning")
            {
                // Take only the first two parts: /learning/course-name
                var mainPath = "/" + string.Join("/", pathParts.Take(2));
                var mainUrl = $"{uri.Scheme}://{uri.Host}{mainPath}";
                
                LogDebug($"  → Extracted main course URL: {mainUrl}");
                return mainUrl;
            }
            
            LogDebug($"  → URL appears to already be main course URL: {url}");
            return url;
        }
        catch (Exception ex)
        {
            LogDebug($"  → Error extracting main course URL: {ex.Message}, using original: {url}");
            return url;
        }
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

    public async Task NavigateToLessonAsync(string lessonUrl, WaitUntilState waitUntil, int timeout, string strategyName, int attempt, int maxRetries)
    {
        if (string.IsNullOrWhiteSpace(lessonUrl))
            throw new ArgumentException("Lesson URL cannot be empty", nameof(lessonUrl));

        if (_page == null)
            throw new InvalidOperationException("Page not initialized. Ensure browser is initialized first.");

        Console.WriteLine($"  → Attempt {attempt}/{maxRetries} using {strategyName} strategy");
        
        try
        {
            // Navigate with the specific strategy
            var response = await _page.GotoAsync(lessonUrl, new PageGotoOptions
            {
                WaitUntil = waitUntil,
                Timeout = timeout
            });

            if (response?.Status == 200)
            {
                try
                {
                    // Wait for video player to be present
                    await _page.WaitForSelectorAsync("video, .classroom-workspace", new PageWaitForSelectorOptions
                    {
                        Timeout = 10000,
                        State = WaitForSelectorState.Attached
                    });
                    Console.WriteLine($"✓ Successfully navigated to lesson with {strategyName}");
                    return;
                }
                catch (TimeoutException)
                {
                    Console.WriteLine($"  → Video player not found, but page loaded (status 200)");
                    // Continue anyway - some lessons might not have video players immediately
                    return;
                }
            }
            else
            {
                throw new Exception($"Navigation returned status: {response?.Status}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Navigation failed with {strategyName}: {ex.Message}");
        }
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

            // Check if we should extract with timestamps
            if (_config.KeepTimestamps)
            {
                return await ExtractWithTimestampsAsync();
            }

            // First, try to get initial content
            var transcriptText = await ExtractTranscriptContentAsync();
            
            // Check if we need to scroll (only for long transcripts)
            if (transcriptText.Length < _config.SinglePassThreshold)
            {
                Console.WriteLine($"  → Single pass extraction (length: {transcriptText.Length} < threshold: {_config.SinglePassThreshold})");
            }
            else
            {
                // For longer transcripts, check if scrolling is needed
                var scrollableContainer = await GetScrollableContainerAsync();
                if (scrollableContainer != null)
                {
                    Console.WriteLine($"  → Long transcript detected, scrolling to load all content...");
                    transcriptText = await ScrollAndExtractAsync(scrollableContainer);
                }
            }

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

    private async Task<string> ExtractTranscriptContentAsync()
    {
        // Check if page is null
        if (_page == null)
        {
            Console.WriteLine("  → Warning: Page is null, cannot extract transcript content");
            return string.Empty;
        }

        // Extract transcript content with multiple selector strategies
        var transcriptText = await _page.EvaluateAsync<string>(@"
            () => {
                // Try multiple selector strategies
                const selectors = [
                    '.classroom-transcript__lines p',
                    '.classroom-transcript__lines',
                    '.transcript-content',
                    '[data-test-id=""transcript-content""]',
                    '.learning-transcript'
                ];
                
                for (const selector of selectors) {
                    const element = document.querySelector(selector);
                    if (element) {
                        const text = element.innerText || element.textContent || '';
                        if (text.trim()) {
                            return text;
                        }
                    }
                }
                
                return '';
            }
        ");

        return transcriptText;
    }

    private async Task<IElementHandle?> GetScrollableContainerAsync()
    {
        // Check if page is null
        if (_page == null)
        {
            Console.WriteLine("  → Warning: Page is null, cannot get scrollable container");
            return null;
        }

        try
        {
            // Try to find scrollable transcript container
            var containerSelectors = new[]
            {
                ".classroom-transcript__lines",
                ".transcript-container",
                ".transcript-scroll-container",
                "[data-test-id='transcript-container']"
            };

            foreach (var selector in containerSelectors)
            {
                var container = await _page.QuerySelectorAsync(selector);
                if (container != null)
                {
                    // Check if container is scrollable
                    var isScrollable = await _page.EvaluateAsync<bool>(@"
                        (element) => {
                            return element.scrollHeight > element.clientHeight;
                        }
                    ", container);

                    if (isScrollable)
                    {
                        Console.WriteLine($"  → Found scrollable container: {selector}");
                        return container;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  → Error checking scrollable container: {ex.Message}");
        }

        return null;
    }

    private async Task<string> ScrollAndExtractAsync(IElementHandle container)
    {
        var allText = new System.Text.StringBuilder();
        var previousLength = 0;
        var scrollRound = 0;
        var noNewContentRounds = 0;

        while (scrollRound < _config.MaxScrollRounds)
        {
            scrollRound++;
            
            // Extract current visible text
            var currentText = await ExtractTranscriptContentAsync();
            
            if (currentText.Length > previousLength)
            {
                Console.WriteLine($"    Round {scrollRound}: Loaded {currentText.Length - previousLength} new characters");
                previousLength = currentText.Length;
                noNewContentRounds = 0;
            }
            else
            {
                noNewContentRounds++;
                if (noNewContentRounds >= 2)
                {
                    Console.WriteLine($"    → No new content for 2 rounds, stopping at round {scrollRound}");
                    break;
                }
            }

            // Scroll down
            if (_page != null)
            {
                await _page.EvaluateAsync(@"
                (element) => {
                    element.scrollTop = element.scrollHeight;
                }
            ", container);
            }

            // Wait for potential new content to load
            await Task.Delay(500);
        }

        if (scrollRound >= _config.MaxScrollRounds)
        {
            Console.WriteLine($"    → Reached maximum scroll rounds ({_config.MaxScrollRounds})");
        }

        // Get final complete text
        return await ExtractTranscriptContentAsync();
    }

    private async Task<string> ExtractWithTimestampsAsync()
    {
        Console.WriteLine("  → Extracting with timestamps enabled...");
        
        // Check if page is null
        if (_page == null)
        {
            Console.WriteLine("  → Warning: Page is null, cannot extract with timestamps");
            return string.Empty;
        }

        try
        {
            // When timestamps are enabled, we need to keep interactive mode ON
            // and extract from the structured elements
            var transcriptWithTimestamps = await _page.EvaluateAsync<string>(@"
                () => {
                    const lines = [];
                    
                    // Try to find timestamp elements
                    const timestampSelectors = [
                        '.classroom-transcript__timestamp',
                        '.transcript-timestamp',
                        '[data-test-id=""transcript-timestamp""]',
                        '.video-transcript__timestamp'
                    ];
                    
                    const textSelectors = [
                        '.classroom-transcript__text',
                        '.transcript-text',
                        '[data-test-id=""transcript-text""]',
                        '.video-transcript__text'
                    ];
                    
                    // Try to find paired timestamp and text elements
                    for (let i = 0; i < timestampSelectors.length; i++) {
                        const timestamps = document.querySelectorAll(timestampSelectors[i]);
                        const texts = document.querySelectorAll(textSelectors[i]);
                        
                        if (timestamps.length > 0 && texts.length > 0) {
                            const count = Math.min(timestamps.length, texts.length);
                            for (let j = 0; j < count; j++) {
                                const timestamp = timestamps[j].innerText || timestamps[j].textContent || '';
                                const text = texts[j].innerText || texts[j].textContent || '';
                                if (timestamp && text) {
                                    lines.push(`[${timestamp.trim()}] ${text.trim()}`);
                                }
                            }
                            break;
                        }
                    }
                    
                    // Fallback: try to extract from any transcript lines with timestamps
                    if (lines.length === 0) {
                        const transcriptLines = document.querySelectorAll('.classroom-transcript__line, .transcript-line');
                        transcriptLines.forEach(line => {
                            const timestamp = line.querySelector('.timestamp, [class*=""timestamp""]');
                            const text = line.querySelector('.text, [class*=""text""]');
                            if (timestamp && text) {
                                lines.push(`[${timestamp.innerText.trim()}] ${text.innerText.trim()}`);
                            }
                        });
                    }
                    
                    return lines.join('\n');
                }
            ");

            if (string.IsNullOrWhiteSpace(transcriptWithTimestamps))
            {
                Console.WriteLine("  → No timestamped content found, falling back to regular extraction");
                return await ExtractTranscriptContentAsync();
            }

            return transcriptWithTimestamps;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  → Error extracting with timestamps: {ex.Message}");
            return await ExtractTranscriptContentAsync();
        }
    }

    private string CleanTranscriptText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Handle different transcript formats
        text = NormalizeTranscriptFormat(text);

        // Remove excessive whitespace while preserving paragraph breaks
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        
        // Clean up speaker indicators (e.g., "- " at the start)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^[\-–—]\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        
        // Remove any potential HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Handle special characters
        text = text.Replace("'", "'").Replace("'", "'");
        text = text.Replace(""", "\"").Replace(""", "\"");
        text = text.Replace("…", "...");
        
        // Trim start and end
        text = text.Trim();

        return text;
    }

    private string NormalizeTranscriptFormat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Detect and handle different transcript formats
        
        // Format 1: Speaker labels (e.g., "[Speaker Name]: text")
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\[[\w\s]+\]:\s"))
        {
            Console.WriteLine("  → Detected speaker label format");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[[\w\s]+\]:\s*", "");
        }
        
        // Format 2: Chapter markers (e.g., "### Chapter Title ###")
        if (text.Contains("###"))
        {
            Console.WriteLine("  → Detected chapter markers");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"#{2,}.*?#{2,}", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        }
        
        // Format 3: Time codes not in brackets (e.g., "00:00 text")
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d{1,2}:\d{2}\s", System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            Console.WriteLine("  → Detected inline time codes");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\d{1,2}:\d{2}\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        }
        
        // Format 4: Numbered lines (e.g., "1. text" or "01 text")
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+[\.\)]\s", System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            Console.WriteLine("  → Detected numbered lines");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\d+[\.\)]\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        return text;
    }


    public async Task<string> ExtractLessonTranscriptAsync(Lesson lesson)
    {
        if (lesson == null)
            throw new ArgumentNullException(nameof(lesson));

        Console.WriteLine($"\n--- Extracting transcript for: {lesson.Title} ---");
        Console.WriteLine($"Navigating to lesson: {lesson.Url}");

        const int maxRetries = 3;
        var waitStrategies = new[]
        {
            new { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000, Name = "NetworkIdle (60s)" },
            new { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000, Name = "DOMContentLoaded (45s)" },
            new { WaitUntil = WaitUntilState.Load, Timeout = 30000, Name = "Load (30s)" }
        };
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Step 1: Navigate to the lesson with current strategy
                var strategy = waitStrategies[Math.Min(attempt - 1, waitStrategies.Length - 1)];
                await NavigateToLessonAsync(lesson.Url, strategy.WaitUntil, strategy.Timeout, strategy.Name, attempt, maxRetries);

                // Step 2: Click the transcript tab
                bool transcriptTabClicked = await ClickTranscriptTabAsync();
                if (!transcriptTabClicked)
                {
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"Attempt {attempt}/{maxRetries}: Transcript tab not found, retrying...");
                        await Task.Delay(2000); // Wait 2 seconds before retry
                        continue; // Try next attempt
                    }
                    else
                    {
                        // All attempts exhausted
                        lesson.HasTranscript = false;
                        lesson.Transcript = string.Empty;
                        Console.WriteLine("No transcript available for this lesson");
                        return string.Empty;
                    }
                }

                // Step 3: Disable interactive transcripts for simpler extraction (unless timestamps are needed)
                if (!_config.KeepTimestamps)
                {
                    await DisableInteractiveTranscriptsAsync();
                }

                // Step 4: Extract the transcript text
                string transcriptText = await ExtractTranscriptTextAsync();

                // Update the lesson object
                lesson.Transcript = transcriptText;
                lesson.HasTranscript = !string.IsNullOrWhiteSpace(transcriptText);
                lesson.ExtractedAt = DateTime.UtcNow;

                if (lesson.HasTranscript)
                {
                    Console.WriteLine($"✓ Successfully extracted transcript for lesson {lesson.LessonNumber} (attempt {attempt}/{maxRetries})");
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
            LogDebug("No lessons to process");
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