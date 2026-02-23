namespace KnotVM.CLI.Extensions;

/// <summary>
/// Extension methods per formattazione valori (dimensioni file, date, etc.)
/// </summary>
public static class FormattingExtensions
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB" };
    
    /// <summary>
    /// Converte bytes in formato human-readable (es: 1024 → "1 KB").
    /// </summary>
    public static string ToHumanReadableSize(this long bytes)
    {
        if (bytes == 0) return "0 B";
        
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < SizeSuffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {SizeSuffixes[order]}";
    }
    
    /// <summary>
    /// Converte DateTime in formato relativo (es: "2 giorni fa").
    /// </summary>
    public static string ToRelativeTime(this DateTime dateTime)
    {
        var timeSpan = DateTime.Now - dateTime;
        
        return timeSpan.TotalDays switch
        {
            < 1 => "oggi",
            < 2 => "ieri",
            < 7 => $"{(int)timeSpan.TotalDays} giorni fa",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)} settimane fa",
            < 365 => $"{(int)(timeSpan.TotalDays / 30)} mesi fa",
            _ => $"{(int)(timeSpan.TotalDays / 365)} anni fa"
        };
    }
}
