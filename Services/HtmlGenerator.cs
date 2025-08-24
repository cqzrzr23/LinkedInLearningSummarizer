using LinkedInLearningSummarizer.Models;
using System.Text;
using System.Web;

namespace LinkedInLearningSummarizer.Services;

public class HtmlGenerator
{
    private readonly AppConfig _config;
    private readonly string _cssContent;

    public HtmlGenerator(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cssContent = GenerateCss();
    }

    /// <summary>
    /// Generates HTML files for the course
    /// </summary>
    public async Task GenerateAsync(Course course)
    {
        if (course == null)
            throw new ArgumentNullException(nameof(course));

        Console.WriteLine($"\n🌐 Generating HTML files for: {course.Title}");

        // Get course directory (same as markdown uses)
        var sanitizedCourseName = SanitizeFilename(course.Title);
        var courseDir = Path.Combine(_config.OutputTranscriptDir, sanitizedCourseName);
        var htmlDir = Path.Combine(courseDir, "html");
        var lessonsDir = Path.Combine(htmlDir, "lessons");

        // Create HTML directories
        Directory.CreateDirectory(htmlDir);
        Directory.CreateDirectory(lessonsDir);

        // Generate CSS file
        await GenerateCssFileAsync(htmlDir);

        // Generate individual lesson files
        await GenerateLessonFilesAsync(course, lessonsDir);

        // Generate index.html
        await GenerateIndexFileAsync(course, htmlDir);

        // Generate full-transcript.html
        await GenerateFullTranscriptAsync(course, htmlDir);

        // Generate AI summary if markdown file exists
        var markdownDir = Path.Combine(courseDir, "markdown");
        var summaryMarkdownPath = Path.Combine(markdownDir, "ai_summary.md");
        if (File.Exists(summaryMarkdownPath))
        {
            var summaryContent = await File.ReadAllTextAsync(summaryMarkdownPath);
            await GenerateAISummaryFileAsync(course, htmlDir, summaryContent);
        }

        // Generate AI review if markdown file exists
        var reviewMarkdownPath = Path.Combine(markdownDir, "ai_review.md");
        if (File.Exists(reviewMarkdownPath))
        {
            var reviewContent = await File.ReadAllTextAsync(reviewMarkdownPath);
            await GenerateAIReviewFileAsync(course, htmlDir, reviewContent);
        }

        Console.WriteLine($"✓ Generated HTML files in: {htmlDir}");
    }

    /// <summary>
    /// Generates CSS file for styling
    /// </summary>
    private async Task GenerateCssFileAsync(string htmlDir)
    {
        var filepath = Path.Combine(htmlDir, "styles.css");
        await File.WriteAllTextAsync(filepath, _cssContent);
    }

    /// <summary>
    /// Generates CSS content based on theme
    /// </summary>
    private string GenerateCss()
    {
        var isDark = _config.HtmlTheme == "dark";
        
        return $@"
/* Base Styles */
:root {{
    --bg-primary: {(isDark ? "#1a1a1a" : "#ffffff")};
    --bg-secondary: {(isDark ? "#2a2a2a" : "#f5f5f5")};
    --text-primary: {(isDark ? "#e0e0e0" : "#333333")};
    --text-secondary: {(isDark ? "#b0b0b0" : "#666666")};
    --link-color: {(isDark ? "#4a9eff" : "#0066cc")};
    --link-hover: {(isDark ? "#6ab3ff" : "#0052a3")};
    --border-color: {(isDark ? "#404040" : "#e0e0e0")};
    --code-bg: {(isDark ? "#2d2d2d" : "#f4f4f4")};
    --missing-lesson: {(isDark ? "#666666" : "#999999")};
}}

* {{
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}}

body {{
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    line-height: 1.6;
    color: var(--text-primary);
    background-color: var(--bg-primary);
    padding: 20px;
}}

.container {{
    max-width: 900px;
    margin: 0 auto;
}}

/* Header Styles */
h1 {{
    color: var(--text-primary);
    border-bottom: 3px solid var(--border-color);
    padding-bottom: 10px;
    margin-bottom: 20px;
}}

h2 {{
    color: var(--text-primary);
    margin-top: 30px;
    margin-bottom: 15px;
    border-bottom: 1px solid var(--border-color);
    padding-bottom: 5px;
}}

h3 {{
    color: var(--text-primary);
    margin-top: 20px;
    margin-bottom: 10px;
}}

/* Link Styles */
a {{
    color: var(--link-color);
    text-decoration: none;
}}

a:hover {{
    color: var(--link-hover);
    text-decoration: underline;
}}

/* Metadata Styles */
.metadata {{
    background-color: var(--bg-secondary);
    padding: 15px;
    border-radius: 5px;
    margin-bottom: 20px;
}}

.metadata p {{
    margin: 5px 0;
    color: var(--text-secondary);
}}

.metadata strong {{
    color: var(--text-primary);
}}

/* Table of Contents */
.toc {{
    background-color: var(--bg-secondary);
    padding: 20px;
    border-radius: 5px;
    margin-bottom: 30px;
}}

.toc ol {{
    counter-reset: lesson-counter;
    list-style: none;
    padding-left: 0;
}}

.toc li {{
    margin: 8px 0;
    padding: 5px 10px;
    border-radius: 3px;
    transition: background-color 0.2s;
}}

.toc li:hover {{
    background-color: var(--bg-primary);
}}

.toc li.missing {{
    color: var(--missing-lesson);
    font-style: italic;
}}

.toc li.missing::after {{
    content: "" (No transcript available)"";
    color: var(--missing-lesson);
    font-size: 0.9em;
    margin-left: 10px;
}}

.toc li::before {{
    counter-increment: lesson-counter;
    content: counter(lesson-counter) "". "";
    font-weight: bold;
    margin-right: 5px;
}}

/* Special numbering for lessons with actual numbers */
.toc li[data-lesson-number]::before {{
    content: attr(data-lesson-number) "". "";
}}

/* Content Styles */
.content {{
    line-height: 1.8;
}}

.content p {{
    margin-bottom: 15px;
}}

/* Code Styles */
pre, code {{
    background-color: var(--code-bg);
    border-radius: 3px;
    font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
}}

code {{
    padding: 2px 5px;
}}

pre {{
    padding: 15px;
    overflow-x: auto;
    margin: 15px 0;
}}

/* Navigation */
.navigation {{
    margin: 30px 0;
    padding: 15px;
    background-color: var(--bg-secondary);
    border-radius: 5px;
    display: flex;
    justify-content: space-between;
    align-items: center;
}}

.navigation a {{
    padding: 8px 15px;
    background-color: var(--link-color);
    color: white;
    border-radius: 3px;
    text-decoration: none;
}}

.navigation a:hover {{
    background-color: var(--link-hover);
}}

/* Statistics */
.statistics {{
    background-color: var(--bg-secondary);
    padding: 20px;
    border-radius: 5px;
    margin-top: 30px;
}}

.statistics h2 {{
    border: none;
    margin-top: 0;
}}

.statistics ul {{
    list-style: none;
    padding-left: 0;
}}

.statistics li {{
    margin: 8px 0;
    padding-left: 20px;
    position: relative;
}}

.statistics li::before {{
    content: ""•"";
    position: absolute;
    left: 0;
    color: var(--link-color);
}}

/* Footer */
.footer {{
    margin-top: 50px;
    padding-top: 20px;
    border-top: 1px solid var(--border-color);
    text-align: center;
    color: var(--text-secondary);
    font-size: 0.9em;
}}

/* Responsive Design */
@media (max-width: 768px) {{
    body {{
        padding: 10px;
    }}
    
    .container {{
        padding: 0 10px;
    }}
    
    .navigation {{
        flex-direction: column;
        gap: 10px;
    }}
}}

/* Print Styles */
@media print {{
    body {{
        background: white;
        color: black;
    }}
    
    .navigation, .footer {{
        display: none;
    }}
}}
";
    }

    /// <summary>
    /// Generates HTML files for all lessons
    /// </summary>
    private async Task GenerateLessonFilesAsync(Course course, string lessonsDir)
    {
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).ToList();

        if (!lessonsWithTranscripts.Any())
        {
            Console.WriteLine("  → No lessons with transcripts to generate");
            return;
        }

        Console.WriteLine($"  → Generating {lessonsWithTranscripts.Count} lesson HTML files...");

        foreach (var lesson in lessonsWithTranscripts)
        {
            await GenerateLessonFileAsync(lesson, course, lessonsDir);
        }
    }

    /// <summary>
    /// Generates a single lesson HTML file
    /// </summary>
    private async Task GenerateLessonFileAsync(Lesson lesson, Course course, string lessonsDir)
    {
        var sanitizedTitle = SanitizeFilename(lesson.Title);
        var filename = $"{lesson.LessonNumber:D2}-{sanitizedTitle}.html";
        var filepath = Path.Combine(lessonsDir, filename);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>Lesson {lesson.LessonNumber}: {HttpUtility.HtmlEncode(lesson.Title)}</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"../styles.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");
        
        // Navigation
        html.AppendLine("        <div class=\"navigation\">");
        html.AppendLine("            <a href=\"../index.html\">← Course Overview</a>");
        html.AppendLine("            <a href=\"../full-transcript.html\">Full Transcript</a>");
        html.AppendLine("        </div>");

        // Header
        html.AppendLine($"        <h1>Lesson {lesson.LessonNumber}: {HttpUtility.HtmlEncode(lesson.Title)}</h1>");
        
        // Metadata
        html.AppendLine("        <div class=\"metadata\">");
        html.AppendLine($"            <p><strong>Course:</strong> {HttpUtility.HtmlEncode(course.Title)}</p>");
        html.AppendLine($"            <p><strong>Instructor:</strong> {HttpUtility.HtmlEncode(course.Instructor)}</p>");
        html.AppendLine($"            <p><strong>Extracted:</strong> {lesson.ExtractedAt:yyyy-MM-dd HH:mm} UTC</p>");
        html.AppendLine("        </div>");

        // AI Summary if available
        if (!string.IsNullOrWhiteSpace(lesson.AISummary))
        {
            html.AppendLine("        <section>");
            html.AppendLine("            <h2>🤖 AI Summary</h2>");
            html.AppendLine($"            <div class=\"content\">{ConvertMarkdownToHtml(lesson.AISummary)}</div>");
            html.AppendLine("        </section>");
        }

        // Transcript
        html.AppendLine("        <section>");
        html.AppendLine("            <h2>Transcript</h2>");
        html.AppendLine($"            <div class=\"content\">{ConvertMarkdownToHtml(lesson.Transcript)}</div>");
        html.AppendLine("        </section>");

        // Footer
        html.AppendLine("        <div class=\"footer\">");
        html.AppendLine("            <p>Generated with LinkedIn Learning AI Course Summarizer</p>");
        html.AppendLine("        </div>");
        
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        await File.WriteAllTextAsync(filepath, html.ToString());
        Console.WriteLine($"    ✓ Generated: {filename}");
    }

    /// <summary>
    /// Generates the course index.html file
    /// </summary>
    private async Task GenerateIndexFileAsync(Course course, string htmlDir)
    {
        var filepath = Path.Combine(htmlDir, "index.html");
        var courseDir = Directory.GetParent(htmlDir)?.FullName ?? "";
        var markdownDir = Path.Combine(courseDir, "markdown");
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{HttpUtility.HtmlEncode(course.Title)}</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"styles.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");
        
        // Header
        html.AppendLine($"        <h1>{HttpUtility.HtmlEncode(course.Title)}</h1>");
        
        // Metadata
        html.AppendLine("        <div class=\"metadata\">");
        html.AppendLine($"            <p><strong>Instructor:</strong> {HttpUtility.HtmlEncode(course.Instructor)}</p>");
        html.AppendLine($"            <p><strong>Total Lessons:</strong> {course.TotalLessons}</p>");
        html.AppendLine($"            <p><strong>Lessons with Transcripts:</strong> {course.Lessons.Count(l => l.HasTranscript)}</p>");
        html.AppendLine($"            <p><strong>Extracted:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>");
        html.AppendLine("        </div>");

        // Course Overview
        html.AppendLine("        <section>");
        html.AppendLine("            <h2>Course Overview</h2>");
        var lessonsWithTranscripts = course.Lessons.Count(l => l.HasTranscript);
        html.AppendLine($"            <p>This course contains {course.TotalLessons} lessons with {lessonsWithTranscripts} lessons having available transcripts.</p>");
        html.AppendLine("        </section>");

        // Table of Contents
        html.AppendLine("        <section>");
        html.AppendLine("            <h2>Table of Contents</h2>");
        html.AppendLine("            <div class=\"toc\">");
        html.AppendLine("                <ol>");
        
        // Generate list with all lessons, marking missing ones
        for (int i = 1; i <= course.TotalLessons; i++)
        {
            var lesson = course.Lessons.FirstOrDefault(l => l.LessonNumber == i);
            if (lesson != null && lesson.HasTranscript)
            {
                var sanitizedTitle = SanitizeFilename(lesson.Title);
                var filename = $"{lesson.LessonNumber:D2}-{sanitizedTitle}.html";
                html.AppendLine($"                    <li data-lesson-number=\"{lesson.LessonNumber}\"><a href=\"lessons/{filename}\">{HttpUtility.HtmlEncode(lesson.Title)}</a></li>");
            }
            else if (lesson != null)
            {
                // Lesson exists but no transcript
                html.AppendLine($"                    <li class=\"missing\" data-lesson-number=\"{lesson.LessonNumber}\">{HttpUtility.HtmlEncode(lesson.Title)}</li>");
            }
        }
        
        html.AppendLine("                </ol>");
        html.AppendLine("            </div>");
        html.AppendLine("        </section>");

        // Quick Links
        html.AppendLine("        <section>");
        html.AppendLine("            <h2>Files</h2>");
        html.AppendLine("            <ul>");
        html.AppendLine("                <li><a href=\"full-transcript.html\">Complete Transcript</a> - All lessons in one file</li>");
        if (File.Exists(Path.Combine(markdownDir, "ai_summary.md")))
            html.AppendLine("                <li><a href=\"ai_summary.html\">AI Course Summary</a></li>");
        if (File.Exists(Path.Combine(markdownDir, "ai_review.md")))
            html.AppendLine("                <li><a href=\"ai_review.html\">AI Course Review</a></li>");
        html.AppendLine("            </ul>");
        html.AppendLine("        </section>");

        // Statistics
        var totalWords = course.Lessons.Where(l => l.HasTranscript).Sum(l => l.Transcript?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0);
        var totalChars = course.Lessons.Where(l => l.HasTranscript).Sum(l => l.Transcript?.Length ?? 0);
        
        html.AppendLine("        <div class=\"statistics\">");
        html.AppendLine("            <h2>Statistics</h2>");
        html.AppendLine("            <ul>");
        html.AppendLine($"                <li><strong>Total Words:</strong> {totalWords:N0}</li>");
        html.AppendLine($"                <li><strong>Total Characters:</strong> {totalChars:N0}</li>");
        html.AppendLine($"                <li><strong>Success Rate:</strong> {(double)lessonsWithTranscripts / course.TotalLessons:P0}</li>");
        html.AppendLine("            </ul>");
        html.AppendLine("        </div>");

        // Footer
        html.AppendLine("        <div class=\"footer\">");
        html.AppendLine("            <p>Generated with LinkedIn Learning AI Course Summarizer</p>");
        html.AppendLine("        </div>");
        
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        await File.WriteAllTextAsync(filepath, html.ToString());
        Console.WriteLine($"    ✓ Generated: index.html");
    }

    /// <summary>
    /// Generates the full transcript HTML file
    /// </summary>
    private async Task GenerateFullTranscriptAsync(Course course, string htmlDir)
    {
        var filepath = Path.Combine(htmlDir, "full-transcript.html");
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).OrderBy(l => l.LessonNumber).ToList();

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{HttpUtility.HtmlEncode(course.Title)} - Complete Transcript</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"styles.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");
        
        // Navigation
        html.AppendLine("        <div class=\"navigation\">");
        html.AppendLine("            <a href=\"index.html\">← Course Overview</a>");
        html.AppendLine("        </div>");

        // Header
        html.AppendLine($"        <h1>{HttpUtility.HtmlEncode(course.Title)} - Complete Transcript</h1>");
        
        // Metadata
        html.AppendLine("        <div class=\"metadata\">");
        html.AppendLine($"            <p><strong>Instructor:</strong> {HttpUtility.HtmlEncode(course.Instructor)}</p>");
        html.AppendLine($"            <p><strong>Total Lessons:</strong> {lessonsWithTranscripts.Count} lessons with transcripts</p>");
        html.AppendLine($"            <p><strong>Generated:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>");
        html.AppendLine("        </div>");

        // Transcripts
        foreach (var lesson in lessonsWithTranscripts)
        {
            html.AppendLine("        <section>");
            html.AppendLine($"            <h2>Lesson {lesson.LessonNumber}: {HttpUtility.HtmlEncode(lesson.Title)}</h2>");
            html.AppendLine($"            <div class=\"content\">{ConvertMarkdownToHtml(lesson.Transcript)}</div>");
            html.AppendLine("        </section>");
        }

        // Footer
        html.AppendLine("        <div class=\"footer\">");
        html.AppendLine("            <p>Generated with LinkedIn Learning AI Course Summarizer</p>");
        html.AppendLine("        </div>");
        
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        await File.WriteAllTextAsync(filepath, html.ToString());
        Console.WriteLine($"    ✓ Generated: full-transcript.html");
    }

    /// <summary>
    /// Generates AI Summary HTML file
    /// </summary>
    private async Task GenerateAISummaryFileAsync(Course course, string htmlDir, string summaryContent)
    {
        var filepath = Path.Combine(htmlDir, "ai_summary.html");
        
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{HttpUtility.HtmlEncode(course.Title)} - AI Summary</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"styles.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");
        
        // Navigation
        html.AppendLine("        <div class=\"navigation\">");
        html.AppendLine("            <a href=\"index.html\">← Course Overview</a>");
        html.AppendLine("        </div>");

        // Header
        html.AppendLine($"        <h1>{HttpUtility.HtmlEncode(course.Title)} - AI Summary</h1>");
        
        // Content
        html.AppendLine("        <div class=\"content\">");
        html.AppendLine(ConvertMarkdownToHtml(summaryContent));
        html.AppendLine("        </div>");

        // Footer
        html.AppendLine("        <div class=\"footer\">");
        html.AppendLine("            <p>AI Summary generated with LinkedIn Learning AI Course Summarizer</p>");
        html.AppendLine("        </div>");
        
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        await File.WriteAllTextAsync(filepath, html.ToString());
        Console.WriteLine($"    ✓ Generated: ai_summary.html");
    }

    /// <summary>
    /// Generates AI Review HTML file
    /// </summary>
    private async Task GenerateAIReviewFileAsync(Course course, string htmlDir, string reviewContent)
    {
        var filepath = Path.Combine(htmlDir, "ai_review.html");
        
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{HttpUtility.HtmlEncode(course.Title)} - AI Review</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"styles.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");
        
        // Navigation
        html.AppendLine("        <div class=\"navigation\">");
        html.AppendLine("            <a href=\"index.html\">← Course Overview</a>");
        html.AppendLine("        </div>");

        // Header
        html.AppendLine($"        <h1>{HttpUtility.HtmlEncode(course.Title)} - AI Review</h1>");
        
        // Content
        html.AppendLine("        <div class=\"content\">");
        html.AppendLine(ConvertMarkdownToHtml(reviewContent));
        html.AppendLine("        </div>");

        // Footer
        html.AppendLine("        <div class=\"footer\">");
        html.AppendLine("            <p>AI Review generated with LinkedIn Learning AI Course Summarizer</p>");
        html.AppendLine("        </div>");
        
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        await File.WriteAllTextAsync(filepath, html.ToString());
        Console.WriteLine($"    ✓ Generated: ai_review.html");
    }

    /// <summary>
    /// Simple markdown to HTML converter
    /// </summary>
    private string ConvertMarkdownToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        // Escape HTML
        var html = HttpUtility.HtmlEncode(markdown);
        
        // Convert line breaks to paragraphs
        var paragraphs = html.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        
        foreach (var paragraph in paragraphs)
        {
            result.AppendLine($"<p>{paragraph.Replace("\n", "<br>")}</p>");
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Sanitizes filename for cross-platform compatibility
    /// </summary>
    private string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "untitled";

        // Remove invalid characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", filename.Where(c => !invalid.Contains(c)));

        // Replace common problematic characters
        sanitized = sanitized.Replace(" ", "-")
                            .Replace(":", "")
                            .Replace("/", "-")
                            .Replace("\\", "-")
                            .Replace("|", "-")
                            .Replace("?", "")
                            .Replace("*", "")
                            .Replace("<", "")
                            .Replace(">", "")
                            .Replace("\"", "");

        // Remove multiple consecutive dashes
        while (sanitized.Contains("--"))
            sanitized = sanitized.Replace("--", "-");

        // Trim dashes from start and end
        sanitized = sanitized.Trim('-');

        // Limit length
        if (sanitized.Length > 100)
            sanitized = sanitized.Substring(0, 100).TrimEnd('-');

        // Ensure not empty
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "untitled";

        return sanitized.ToLowerInvariant();
    }
}