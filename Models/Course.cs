namespace LinkedInLearningSummarizer.Models;

public class Course
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalLessons { get; set; }
    public TimeSpan Duration { get; set; }
    public List<Lesson> Lessons { get; set; } = new();
    public string AISummary { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}