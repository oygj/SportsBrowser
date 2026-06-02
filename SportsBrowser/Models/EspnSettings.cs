namespace SportsBrowser.Models;

public sealed class EspnSettings
{
    public string BaseUrl { get; set; } = null!;
    public LeagueInfo[] Leagues { get; set; } = Array.Empty<LeagueInfo>();
}
