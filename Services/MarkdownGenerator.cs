using LinkedInLearningSummarizer.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace LinkedInLearningSummarizer.Services;

public class MarkdownGenerator
{
    private readonly AppConfig _config;

    public MarkdownGenerator(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Generates complete markdown file structure for a course
    /// </summary>
    public async Task GenerateAsync(Course course)
    {
        if (course == null)
            throw new ArgumentNullException(nameof(course));

        Console.WriteLine($"\n📝 Generating markdown files for: {course.Title}");

        // Create course directory structure
        var courseDir = await CreateCourseDirectoryAsync(course);
        
        // Generate individual lesson files
        await GenerateLessonFilesAsync(course, courseDir);

        // Generate course README.md
        await GenerateCourseReadmeAsync(course, courseDir);

        // Generate full-transcript.md
        await GenerateFullTranscriptAsync(course, courseDir);

        Console.WriteLine($"✓ Generated markdown files in: {courseDir}");
    }
    
    /// <summary>
    /// Generates enhanced markdown files with AI summaries
    /// </summary>
    public async Task GenerateWithAISummariesAsync(Course course, string courseSummary)
    {
        if (course == null)
            throw new ArgumentNullException(nameof(course));

        Console.WriteLine($"\n📝 Generating enhanced markdown files with AI summaries for: {course.Title}");

        // Create course directory structure
        var courseDir = await CreateCourseDirectoryAsync(course);
        
        // Generate enhanced lesson files with AI summaries
        await GenerateEnhancedLessonFilesAsync(course, courseDir, courseSummary);

        // Generate enhanced course README.md with AI summary
        await GenerateEnhancedCourseReadmeAsync(course, courseDir, courseSummary);

        // Generate full-transcript.md (same as before)
        await GenerateFullTranscriptAsync(course, courseDir);

        Console.WriteLine($"✓ Generated enhanced markdown files with AI summaries in: {courseDir}");
    }

    /// <summary>
    /// Creates the course directory structure and returns the base path
    /// </summary>
    private Task<string> CreateCourseDirectoryAsync(Course course)
    {
        // Sanitize course name for directory
        var sanitizedCourseName = SanitizeFilename(course.Title);
        var courseDir = Path.Combine(_config.OutputTranscriptDir, sanitizedCourseName);
        var lessonsDir = Path.Combine(courseDir, "lessons");

        // Create directories
        Directory.CreateDirectory(courseDir);
        Directory.CreateDirectory(lessonsDir);

        Console.WriteLine($"  → Created directory structure: {courseDir}");
        return Task.FromResult(courseDir);
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
        sanitized = Regex.Replace(sanitized, @"-+", "-");

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

    /// <summary>
    /// Generates markdown files for all lessons
    /// </summary>
    private async Task GenerateLessonFilesAsync(Course course, string courseDir)
    {
        var lessonsDir = Path.Combine(courseDir, "lessons");
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).ToList();

        if (!lessonsWithTranscripts.Any())
        {
            Console.WriteLine("  → No lessons with transcripts to generate");
            return;
        }

        Console.WriteLine($"  → Generating {lessonsWithTranscripts.Count} lesson files...");

        for (int i = 0; i < lessonsWithTranscripts.Count; i++)
        {
            var lesson = lessonsWithTranscripts[i];
            await GenerateLessonFileAsync(lesson, course, lessonsDir, i, lessonsWithTranscripts.Count);
        }
    }

    /// <summary>
    /// Generates a single lesson markdown file
    /// </summary>
    private async Task GenerateLessonFileAsync(Lesson lesson, Course course, string lessonsDir, int index, int totalCount)
    {
        var sanitizedTitle = SanitizeFilename(lesson.Title);
        var filename = $"{(index + 1):D2}-{sanitizedTitle}.md";
        var filepath = Path.Combine(lessonsDir, filename);

        var content = new StringBuilder();

        // Header with lesson metadata
        content.AppendLine($"# Lesson {lesson.LessonNumber}: {lesson.Title}");
        content.AppendLine();
        content.AppendLine($"**Course:** {course.Title}");
        content.AppendLine($"**Instructor:** {course.Instructor}");
        content.AppendLine($"**Lesson:** {index + 1} of {totalCount}");
        if (lesson.Duration != TimeSpan.Zero)
            content.AppendLine($"**Duration:** {FormatDuration(lesson.Duration)}");
        content.AppendLine($"**Extracted:** {lesson.ExtractedAt:yyyy-MM-dd HH:mm} UTC");
        content.AppendLine();

        // Navigation links
        var navigation = BuildLessonNavigation(index, totalCount, course.Lessons.Where(l => l.HasTranscript).ToList());
        if (!string.IsNullOrEmpty(navigation))
        {
            content.AppendLine(navigation);
            content.AppendLine();
        }

        // Transcript content
        content.AppendLine("## Transcript");
        content.AppendLine();
        content.AppendLine(lesson.Transcript);
        content.AppendLine();

        // Footer navigation
        content.AppendLine("---");
        content.AppendLine();
        content.AppendLine("**Navigation:**");
        if (index > 0)
        {
            var prevLesson = course.Lessons.Where(l => l.HasTranscript).ToList()[index - 1];
            var prevSanitized = SanitizeFilename(prevLesson.Title);
            var prevFilename = $"{index:D2}-{prevSanitized}.md";
            content.AppendLine($"- [← Previous: {prevLesson.Title}]({prevFilename})");
        }
        content.AppendLine("- [Course Overview](../README.md)");
        if (index < totalCount - 1)
        {
            var nextLesson = course.Lessons.Where(l => l.HasTranscript).ToList()[index + 1];
            var nextSanitized = SanitizeFilename(nextLesson.Title);
            var nextFilename = $"{(index + 2):D2}-{nextSanitized}.md";
            content.AppendLine($"- [Next: {nextLesson.Title} →]({nextFilename})");
        }
        content.AppendLine("- [Complete Transcript](../full-transcript.md)");

        await File.WriteAllTextAsync(filepath, content.ToString());
        Console.WriteLine($"    ✓ Generated: {filename}");
    }

    /// <summary>
    /// Builds navigation links for lesson header
    /// </summary>
    private string BuildLessonNavigation(int currentIndex, int totalCount, List<Lesson> lessonsWithTranscripts)
    {
        var nav = new StringBuilder();
        
        if (currentIndex > 0)
        {
            var prevLesson = lessonsWithTranscripts[currentIndex - 1];
            var prevSanitized = SanitizeFilename(prevLesson.Title);
            var prevFilename = $"{currentIndex:D2}-{prevSanitized}.md";
            nav.Append($"[← Previous: {prevLesson.Title}]({prevFilename})");
        }
        
        if (currentIndex > 0 && currentIndex < totalCount - 1)
            nav.Append(" | ");
        
        nav.Append("[Course README](../README.md)");
        
        if (currentIndex < totalCount - 1)
        {
            var nextLesson = lessonsWithTranscripts[currentIndex + 1];
            var nextSanitized = SanitizeFilename(nextLesson.Title);
            var nextFilename = $"{(currentIndex + 2):D2}-{nextSanitized}.md";
            nav.Append($" | [Next: {nextLesson.Title} →]({nextFilename})");
        }

        return nav.ToString();
    }

    /// <summary>
    /// Generates the course README.md file
    /// </summary>
    private async Task GenerateCourseReadmeAsync(Course course, string courseDir)
    {
        var filepath = Path.Combine(courseDir, "README.md");
        var content = new StringBuilder();
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).ToList();

        // Course header
        content.AppendLine($"# {course.Title}");
        content.AppendLine();
        
        // Course metadata
        content.AppendLine($"**Instructor:** {course.Instructor}");
        content.AppendLine($"**Total Lessons:** {course.TotalLessons} lessons");
        content.AppendLine($"**Lessons with Transcripts:** {lessonsWithTranscripts.Count}");
        content.AppendLine($"**Extracted:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        content.AppendLine();

        // Course overview
        content.AppendLine("## Course Overview");
        content.AppendLine($"This course contains {course.TotalLessons} lessons with {lessonsWithTranscripts.Count} lessons having available transcripts.");
        if (!string.IsNullOrWhiteSpace(course.Description))
        {
            content.AppendLine();
            content.AppendLine(course.Description);
        }
        content.AppendLine();

        // Table of contents
        content.AppendLine("## Table of Contents");
        content.AppendLine();
        
        if (lessonsWithTranscripts.Any())
        {
            for (int i = 0; i < lessonsWithTranscripts.Count; i++)
            {
                var lesson = lessonsWithTranscripts[i];
                var sanitizedTitle = SanitizeFilename(lesson.Title);
                var filename = $"{(i + 1):D2}-{sanitizedTitle}.md";
                var duration = lesson.Duration != TimeSpan.Zero ? $" ({FormatDuration(lesson.Duration)})" : "";
                content.AppendLine($"{i + 1}. [{lesson.Title}](lessons/{filename}){duration}");
            }
        }
        else
        {
            content.AppendLine("*No lessons with transcripts available.*");
        }
        content.AppendLine();

        // Files section
        content.AppendLine("## Files");
        content.AppendLine("- [Complete Transcript](full-transcript.md) - All lessons in one file");
        content.AppendLine("- [Lessons](lessons/) - Individual lesson files");
        content.AppendLine();

        // Statistics
        content.AppendLine("## Statistics");
        var totalWords = lessonsWithTranscripts.Sum(l => l.Transcript?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0);
        var totalChars = lessonsWithTranscripts.Sum(l => l.Transcript?.Length ?? 0);
        content.AppendLine($"- **Total Words:** {totalWords:N0}");
        content.AppendLine($"- **Total Characters:** {totalChars:N0}");
        content.AppendLine($"- **Success Rate:** {(double)lessonsWithTranscripts.Count / course.TotalLessons:P0}");
        content.AppendLine();

        // Footer
        content.AppendLine("---");
        content.AppendLine("*Generated with [LinkedIn Learning AI Course Summarizer](https://github.com/user/linkedin-learning-summarizer)*");

        await File.WriteAllTextAsync(filepath, content.ToString());
        Console.WriteLine($"    ✓ Generated: README.md");
    }

    /// <summary>
    /// Generates the full-transcript.md file
    /// </summary>
    private async Task GenerateFullTranscriptAsync(Course course, string courseDir)
    {
        var filepath = Path.Combine(courseDir, "full-transcript.md");
        var content = new StringBuilder();
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).OrderBy(l => l.LessonNumber).ToList();

        // Header
        content.AppendLine($"# {course.Title} - Complete Transcript");
        content.AppendLine();
        content.AppendLine($"**Instructor:** {course.Instructor}");
        content.AppendLine($"**Total Lessons:** {lessonsWithTranscripts.Count} lessons with transcripts");
        content.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        content.AppendLine();

        // Table of contents with jump links
        content.AppendLine("## Table of Contents");
        content.AppendLine();
        for (int i = 0; i < lessonsWithTranscripts.Count; i++)
        {
            var lesson = lessonsWithTranscripts[i];
            var anchor = SanitizeAnchor(lesson.Title);
            var duration = lesson.Duration != TimeSpan.Zero ? $" ({FormatDuration(lesson.Duration)})" : "";
            content.AppendLine($"{i + 1}. [Lesson {lesson.LessonNumber}: {lesson.Title}](#lesson-{lesson.LessonNumber}-{anchor}){duration}");
        }
        content.AppendLine();

        // Navigation
        content.AppendLine("**Files:**");
        content.AppendLine("- [Course Overview](README.md)");
        content.AppendLine("- [Individual Lessons](lessons/)");
        content.AppendLine();

        // Full transcript content
        content.AppendLine("---");
        content.AppendLine();

        foreach (var lesson in lessonsWithTranscripts)
        {
            var anchor = SanitizeAnchor(lesson.Title);
            content.AppendLine($"## Lesson {lesson.LessonNumber}: {lesson.Title}");
            content.AppendLine();
            
            // Lesson metadata
            var sanitizedTitle = SanitizeFilename(lesson.Title);
            var lessonIndex = lessonsWithTranscripts.IndexOf(lesson);
            var filename = $"{(lessonIndex + 1):D2}-{sanitizedTitle}.md";
            content.AppendLine($"**Individual File:** [lessons/{filename}](lessons/{filename})");
            if (lesson.Duration != TimeSpan.Zero)
                content.AppendLine($"**Duration:** {FormatDuration(lesson.Duration)}");
            content.AppendLine();
            
            // Transcript content
            content.AppendLine(lesson.Transcript);
            content.AppendLine();
            content.AppendLine("---");
            content.AppendLine();
        }

        // Footer
        content.AppendLine("*Generated with [LinkedIn Learning AI Course Summarizer](https://github.com/user/linkedin-learning-summarizer)*");

        await File.WriteAllTextAsync(filepath, content.ToString());
        Console.WriteLine($"    ✓ Generated: full-transcript.md");
    }

    /// <summary>
    /// Sanitizes text for use as markdown anchor links
    /// </summary>
    private string SanitizeAnchor(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "section";

        // Convert to lowercase and replace spaces with hyphens
        var anchor = text.ToLowerInvariant()
                        .Replace(" ", "-")
                        .Replace(":", "")
                        .Replace("?", "")
                        .Replace("!", "")
                        .Replace(".", "")
                        .Replace(",", "")
                        .Replace("(", "")
                        .Replace(")", "")
                        .Replace("[", "")
                        .Replace("]", "")
                        .Replace("/", "-")
                        .Replace("\\", "-");

        // Remove multiple consecutive hyphens
        anchor = Regex.Replace(anchor, @"-+", "-");
        anchor = anchor.Trim('-');

        return anchor;
    }

    /// <summary>
    /// Formats duration for display
    /// </summary>
    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
            return $"{duration.Seconds}s";
        if (duration.TotalHours < 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{(int)duration.TotalHours}h {duration.Minutes}m";
    }

    /// <summary>
    /// Generates enhanced lesson files with AI summaries
    /// </summary>
    private async Task GenerateEnhancedLessonFilesAsync(Course course, string courseDir, string courseSummary)
    {
        var lessonsDir = Path.Combine(courseDir, "lessons");
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).ToList();
        
        if (!lessonsWithTranscripts.Any())
        {
            Console.WriteLine("  → No lessons with transcripts to generate");
            return;
        }

        Console.WriteLine($"  → Generating {lessonsWithTranscripts.Count} enhanced lesson files...");
        
        for (int i = 0; i < lessonsWithTranscripts.Count; i++)
        {
            var lesson = lessonsWithTranscripts[i];
            await GenerateEnhancedLessonFileAsync(lesson, course, lessonsDir, i, lessonsWithTranscripts.Count);
        }
    }

    /// <summary>
    /// Generates enhanced individual lesson file with AI summary
    /// </summary>
    private async Task GenerateEnhancedLessonFileAsync(Lesson lesson, Course course, string lessonsDir, int index, int totalCount)
    {
        var sanitizedTitle = SanitizeFilename(lesson.Title);
        var filename = $"{lesson.LessonNumber:D2}-{sanitizedTitle}.md";
        var filePath = Path.Combine(lessonsDir, filename);

        var content = new StringBuilder();
        
        // Lesson header
        content.AppendLine($"# {lesson.Title}");
        content.AppendLine();
        content.AppendLine($"**Course:** [{course.Title}](../README.md)");
        content.AppendLine($"**Instructor:** {course.Instructor}");
        content.AppendLine($"**Lesson:** {lesson.LessonNumber} of {course.TotalLessons}");
        content.AppendLine($"**Duration:** {FormatDuration(lesson.Duration)}");
        content.AppendLine($"**Extracted:** {lesson.ExtractedAt:yyyy-MM-dd HH:mm}");
        content.AppendLine();
        
        // Navigation
        var navigation = BuildLessonNavigation(index, totalCount, course.Lessons.Where(l => l.HasTranscript).ToList());
        content.AppendLine(navigation);
        content.AppendLine();
        
        // AI Summary (if available)
        if (!string.IsNullOrWhiteSpace(lesson.AISummary))
        {
            content.AppendLine("## 🤖 AI Summary");
            content.AppendLine();
            content.AppendLine(lesson.AISummary);
            content.AppendLine();
            content.AppendLine("---");
            content.AppendLine();
        }
        
        // Transcript content
        content.AppendLine("## Transcript");
        content.AppendLine();
        content.AppendLine(lesson.Transcript);
        content.AppendLine();

        // Footer navigation
        content.AppendLine("## Navigation");
        content.AppendLine();
        if (index > 0)
        {
            var prevLesson = course.Lessons.Where(l => l.HasTranscript).ToList()[index - 1];
            var prevSanitized = SanitizeFilename(prevLesson.Title);
            var prevFile = $"{prevLesson.LessonNumber:D2}-{prevSanitized}.md";
            content.AppendLine($"- [⬅ Previous: {prevLesson.Title}]({prevFile})");
        }
        
        if (index < totalCount - 1)
        {
            var nextLesson = course.Lessons.Where(l => l.HasTranscript).ToList()[index + 1];
            var nextSanitized = SanitizeFilename(nextLesson.Title);
            var nextFile = $"{nextLesson.LessonNumber:D2}-{nextSanitized}.md";
            content.AppendLine($"- [Next: {nextLesson.Title} ➡]({nextFile})");
        }
        
        content.AppendLine("- [📋 Course Overview](../README.md)");
        content.AppendLine("- [📄 Complete Transcript](../full-transcript.md)");

        await File.WriteAllTextAsync(filePath, content.ToString());
    }

    /// <summary>
    /// Generates enhanced course README with AI summary
    /// </summary>
    private async Task GenerateEnhancedCourseReadmeAsync(Course course, string courseDir, string courseSummary)
    {
        var filePath = Path.Combine(courseDir, "README.md");
        var lessonsWithTranscripts = course.Lessons.Where(l => l.HasTranscript).ToList();

        var content = new StringBuilder();
        
        // Course header
        content.AppendLine($"# {course.Title}");
        content.AppendLine();
        content.AppendLine($"**Instructor:** {course.Instructor}");
        content.AppendLine($"**URL:** {course.Url}");
        content.AppendLine($"**Total Lessons:** {course.TotalLessons}");
        content.AppendLine($"**Lessons with Transcripts:** {lessonsWithTranscripts.Count}");
        content.AppendLine($"**Extracted:** {DateTime.Now:yyyy-MM-dd HH:mm}");
        content.AppendLine();

        // Course AI Summary (if available)
        if (!string.IsNullOrWhiteSpace(courseSummary))
        {
            content.AppendLine("## 🤖 AI Course Summary");
            content.AppendLine();
            content.AppendLine(courseSummary);
            content.AppendLine();
            content.AppendLine("---");
            content.AppendLine();
        }

        // Course description
        content.AppendLine("## Course Overview");
        content.AppendLine();
        content.AppendLine($"This course contains {course.TotalLessons} lessons with {lessonsWithTranscripts.Count} lessons having available transcripts.");
        
        if (lessonsWithTranscripts.Count < course.TotalLessons)
        {
            content.AppendLine($"");
            content.AppendLine($"**Note:** {course.TotalLessons - lessonsWithTranscripts.Count} lessons do not have transcripts available and were skipped during extraction.");
        }
        
        content.AppendLine();

        // Table of contents
        content.AppendLine("## 📚 Table of Contents");
        content.AppendLine();
        if (lessonsWithTranscripts.Any())
        {
            for (int i = 0; i < lessonsWithTranscripts.Count; i++)
            {
                var lesson = lessonsWithTranscripts[i];
                var sanitizedTitle = SanitizeFilename(lesson.Title);
                var filename = $"{lesson.LessonNumber:D2}-{sanitizedTitle}.md";
                var hasSummary = !string.IsNullOrWhiteSpace(lesson.AISummary) ? " 🤖" : "";
                content.AppendLine($"{i + 1}. [{lesson.Title}](lessons/{filename}) ({FormatDuration(lesson.Duration)}){hasSummary}");
            }
        }
        else
        {
            content.AppendLine("*No lessons with available transcripts*");
        }
        content.AppendLine();

        // Quick links
        content.AppendLine("## 🔗 Quick Links");
        content.AppendLine();
        content.AppendLine("- [📄 Complete Transcript](full-transcript.md) - All lessons in one file");
        content.AppendLine();

        // Statistics
        var totalWords = lessonsWithTranscripts.Sum(l => l.Transcript?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0);
        var totalChars = lessonsWithTranscripts.Sum(l => l.Transcript?.Length ?? 0);
        var lessonsWithAI = lessonsWithTranscripts.Count(l => !string.IsNullOrWhiteSpace(l.AISummary));
        
        content.AppendLine("## 📊 Statistics");
        content.AppendLine();
        content.AppendLine($"- **Success Rate:** {(double)lessonsWithTranscripts.Count / course.TotalLessons:P0}");
        content.AppendLine($"- **Total Words:** {totalWords:N0}");
        content.AppendLine($"- **Total Characters:** {totalChars:N0}");
        content.AppendLine($"- **Lessons with AI Summaries:** {lessonsWithAI}/{lessonsWithTranscripts.Count}");
        content.AppendLine($"- **Course AI Summary:** {(!string.IsNullOrWhiteSpace(courseSummary) ? "✅" : "❌")}");

        await File.WriteAllTextAsync(filePath, content.ToString());
        Console.WriteLine($"  → Generated enhanced course README: {filePath}");
    }
}