namespace LinkedInLearningSummarizer.Models;

public class Lesson
{
    public int LessonNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string Transcript { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool HasTranscript { get; set; }
    public DateTime ExtractedAt { get; set; }
}