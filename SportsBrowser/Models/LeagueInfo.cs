namespace SportsBrowser.Models;

public sealed class LeagueInfo
{
    public string Sport { get; set; } = null!;
    public string LeaguePath { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string[] TeamNameKeywords { get; set; } = Array.Empty<string>();
}
