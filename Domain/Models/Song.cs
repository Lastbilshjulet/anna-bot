using System;
using System.Text.RegularExpressions;

namespace anna_bot.Domain.Models;

public partial class Song
{
    public string YoutubeId { get; set; } = string.Empty;
    public string? SpotifyId { get; set; }
    public string Title { get; set; } = null!;
    public string Artist { get; set; } = null!;
    public string Thumbnail { get; set; } = string.Empty;
    public string Source { get; set; } = null!;
    public string Path { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = null!;
    public ulong RequestedByUserId { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public int TimesPlayed { get; set; }
    public bool Autoplay { get; set; } = true;
    public int TimesAutoPlayed { get; set; }
    public bool IsAutoPlayed { get; set; }
    
    public string FormattedDuration() => Duration.ToString(Duration.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss");

    public string CleanTitle()
    {
        return CleanTitleRegex().Replace(Title, "_");
    }

    public string GetFullPath(string absolutePath, string? extension = null)
    {
        if (extension != null)
            return System.IO.Path.Combine(absolutePath, $"{CleanTitle()}{extension}");
        
        return System.IO.Path.Combine(absolutePath, $"{CleanTitle()}{Extension}");
    }

    public string GetYouTubeUrl()
    {
        return $"https://www.youtube.com/watch?v={YoutubeId}";
    }
    
    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex CleanTitleRegex();
}
