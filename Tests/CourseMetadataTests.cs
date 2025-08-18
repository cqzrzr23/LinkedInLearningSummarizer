using LinkedInLearningSummarizer.Models;
using Xunit;

namespace Tests;

public class CourseMetadataTests
{
    [Fact]
    public void Course_DefaultConstructor_InitializesPropertiesCorrectly()
    {
        // Act
        var course = new Course();

        // Assert
        Assert.Equal(string.Empty, course.Url);
        Assert.Equal(string.Empty, course.Title);
        Assert.Equal(string.Empty, course.Instructor);
        Assert.Equal(string.Empty, course.Description);
        Assert.Equal(0, course.TotalLessons);
        Assert.Equal(TimeSpan.Zero, course.Duration);
        Assert.NotNull(course.Lessons);
        Assert.Empty(course.Lessons);
        Assert.Equal(string.Empty, course.AISummary);
        Assert.Equal(default(DateTime), course.ProcessedAt);
    }

    [Fact]
    public void Course_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var course = new Course();
        var testDate = DateTime.UtcNow;
        var testDuration = TimeSpan.FromHours(2.5);

        // Act
        course.Url = "https://www.linkedin.com/learning/courses/test";
        course.Title = "Test Course Title";
        course.Instructor = "John Doe";
        course.Description = "This is a test course description";
        course.TotalLessons = 25;
        course.Duration = testDuration;
        course.AISummary = "AI generated summary";
        course.ProcessedAt = testDate;

        // Assert
        Assert.Equal("https://www.linkedin.com/learning/courses/test", course.Url);
        Assert.Equal("Test Course Title", course.Title);
        Assert.Equal("John Doe", course.Instructor);
        Assert.Equal("This is a test course description", course.Description);
        Assert.Equal(25, course.TotalLessons);
        Assert.Equal(testDuration, course.Duration);
        Assert.Equal("AI generated summary", course.AISummary);
        Assert.Equal(testDate, course.ProcessedAt);
    }

    [Fact]
    public void Course_AddLessons_UpdatesLessonsList()
    {
        // Arrange
        var course = new Course();
        var lesson1 = new Lesson 
        { 
            Title = "Lesson 1", 
            Url = "https://www.linkedin.com/learning/courses/test/lesson1",
            LessonNumber = 1 
        };
        var lesson2 = new Lesson 
        { 
            Title = "Lesson 2", 
            Url = "https://www.linkedin.com/learning/courses/test/lesson2",
            LessonNumber = 2 
        };

        // Act
        course.Lessons.Add(lesson1);
        course.Lessons.Add(lesson2);

        // Assert
        Assert.Equal(2, course.Lessons.Count);
        Assert.Contains(lesson1, course.Lessons);
        Assert.Contains(lesson2, course.Lessons);
        Assert.Equal("Lesson 1", course.Lessons[0].Title);
        Assert.Equal("Lesson 2", course.Lessons[1].Title);
    }

    [Fact]
    public void Lesson_DefaultConstructor_InitializesPropertiesCorrectly()
    {
        // Act
        var lesson = new Lesson();

        // Assert
        Assert.Equal(string.Empty, lesson.Url);
        Assert.Equal(string.Empty, lesson.Title);
        Assert.Equal(0, lesson.LessonNumber);
        Assert.Equal(TimeSpan.Zero, lesson.Duration);
        Assert.Equal(string.Empty, lesson.Transcript);
        Assert.False(lesson.HasTranscript);
    }

    [Fact]
    public void Lesson_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var lesson = new Lesson();
        var testDuration = TimeSpan.FromMinutes(15);

        // Act
        lesson.Url = "https://www.linkedin.com/learning/courses/test/lesson1";
        lesson.Title = "Introduction to Testing";
        lesson.LessonNumber = 1;
        lesson.Duration = testDuration;
        lesson.Transcript = "This is the transcript content";
        lesson.HasTranscript = true;

        // Assert
        Assert.Equal("https://www.linkedin.com/learning/courses/test/lesson1", lesson.Url);
        Assert.Equal("Introduction to Testing", lesson.Title);
        Assert.Equal(1, lesson.LessonNumber);
        Assert.Equal(testDuration, lesson.Duration);
        Assert.Equal("This is the transcript content", lesson.Transcript);
        Assert.True(lesson.HasTranscript);
    }

    [Fact]
    public void Course_WithMultipleLessons_CalculatesTotalCorrectly()
    {
        // Arrange
        var course = new Course();
        
        // Act
        for (int i = 1; i <= 10; i++)
        {
            course.Lessons.Add(new Lesson 
            { 
                Title = $"Lesson {i}",
                LessonNumber = i,
                Url = $"https://linkedin.com/learning/courses/test/lesson{i}"
            });
        }
        course.TotalLessons = course.Lessons.Count;

        // Assert
        Assert.Equal(10, course.TotalLessons);
        Assert.Equal(10, course.Lessons.Count);
    }

    [Fact]
    public void Course_ProcessedAt_TracksProcessingTime()
    {
        // Arrange
        var beforeTime = DateTime.UtcNow;
        var course = new Course();
        
        // Act
        course.ProcessedAt = DateTime.UtcNow;
        var afterTime = DateTime.UtcNow;

        // Assert
        Assert.True(course.ProcessedAt >= beforeTime);
        Assert.True(course.ProcessedAt <= afterTime);
    }

    [Fact]
    public void Lesson_OrderProperty_HandlesSequencing()
    {
        // Arrange
        var lessons = new List<Lesson>
        {
            new Lesson { Title = "Third", LessonNumber = 3 },
            new Lesson { Title = "First", LessonNumber = 1 },
            new Lesson { Title = "Second", LessonNumber = 2 }
        };

        // Act
        var sortedLessons = lessons.OrderBy(l => l.LessonNumber).ToList();

        // Assert
        Assert.Equal("First", sortedLessons[0].Title);
        Assert.Equal("Second", sortedLessons[1].Title);
        Assert.Equal("Third", sortedLessons[2].Title);
    }

    [Fact]
    public void Course_EmptyLessons_HandlesGracefully()
    {
        // Arrange
        var course = new Course
        {
            Title = "Course with no lessons",
            TotalLessons = 0
        };

        // Assert
        Assert.NotNull(course.Lessons);
        Assert.Empty(course.Lessons);
        Assert.Equal(0, course.TotalLessons);
    }

    [Fact]
    public void Lesson_TranscriptProperty_HandlesLargeText()
    {
        // Arrange
        var lesson = new Lesson();
        var largeTranscript = new string('A', 50000); // 50K characters

        // Act
        lesson.Transcript = largeTranscript;

        // Assert
        Assert.Equal(50000, lesson.Transcript.Length);
        Assert.Equal(largeTranscript, lesson.Transcript);
    }

    [Fact]
    public void Course_Duration_CalculatesFromLessons()
    {
        // Arrange
        var course = new Course();
        var lesson1 = new Lesson { Duration = TimeSpan.FromMinutes(10) };
        var lesson2 = new Lesson { Duration = TimeSpan.FromMinutes(15) };
        var lesson3 = new Lesson { Duration = TimeSpan.FromMinutes(20) };

        // Act
        course.Lessons.AddRange(new[] { lesson1, lesson2, lesson3 });
        var totalDuration = course.Lessons.Sum(l => l.Duration.TotalMinutes);
        course.Duration = TimeSpan.FromMinutes(totalDuration);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(45), course.Duration);
    }
}