using System;
using System.IO;
using System.Text.RegularExpressions;

namespace anna_bot.Domain.Models;

public partial class Song
{
    public string YoutubeId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Artist { get; set; } = null!;
    public string Thumbnail { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string Path { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = null!;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public int TimesPlayed { get; set; }
    public bool Autoplay { get; set; } = true;
    public int TimesAutoPlayed { get; set; }
    public bool IsAutoPlayed { get; set; }

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

    public void IncrementPlayCount() => TimesPlayed++;
    public void IncrementAutoPlayCount() => TimesAutoPlayed++;
    
    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex CleanTitleRegex();
}
