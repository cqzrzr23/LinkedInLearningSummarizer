using LinkedInLearningSummarizer.Models;

namespace LinkedInLearningSummarizer.Utils;

public static class UrlFileProcessor
{
    public static async Task<UrlFileResult> ProcessUrlFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new UrlFileResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Urls = new List<string>()
                };
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            var validUrls = ParseUrls(lines);

            return new UrlFileResult
            {
                IsSuccess = true,
                Urls = validUrls,
                TotalLinesProcessed = lines.Length,
                ValidUrlCount = validUrls.Count
            };
        }
        catch (Exception ex)
        {
            return new UrlFileResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Urls = new List<string>()
            };
        }
    }

    public static List<string> ParseUrls(IEnumerable<string> lines)
    {
        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
            .Select(line => line.Trim())
            .ToList();
    }

    public static bool IsValidLinkedInLearningUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && 
               url.Contains("linkedin.com/learning", StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> FilterValidLinkedInUrls(IEnumerable<string> urls)
    {
        return urls.Where(IsValidLinkedInLearningUrl).ToList();
    }

    public static UrlValidationResult ValidateUrls(IEnumerable<string> urls)
    {
        var urlList = urls.ToList();
        var validUrls = new List<string>();
        var invalidUrls = new List<string>();

        foreach (var url in urlList)
        {
            if (IsValidLinkedInLearningUrl(url))
            {
                validUrls.Add(url);
            }
            else
            {
                invalidUrls.Add(url);
            }
        }

        return new UrlValidationResult
        {
            ValidUrls = validUrls,
            InvalidUrls = invalidUrls,
            TotalProcessed = urlList.Count,
            ValidCount = validUrls.Count,
            InvalidCount = invalidUrls.Count
        };
    }
}

public class UrlFileResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Urls { get; set; } = new();
    public int TotalLinesProcessed { get; set; }
    public int ValidUrlCount { get; set; }
}

public class UrlValidationResult
{
    public List<string> ValidUrls { get; set; } = new();
    public List<string> InvalidUrls { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }
}