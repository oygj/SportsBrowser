namespace SportsBrowser.Models;

public sealed class GameEventDto
{
    public string GameId { get; set; } = null!;
    public string Sport { get; set; } = null!;
    public string LeaguePath { get; set; } = null!;
    public string TeamDisplayName { get; set; } = null!;
    public string OpponentDisplayName { get; set; } = null!;
    public bool IsHome { get; set; }
    public string HomeTeam { get; set; } = null!;
    public string AwayTeam { get; set; } = null!;
    public string HomeTeamAbbreviation { get; set; } = null!;
    public string AwayTeamAbbreviation { get; set; } = null!;
    public string? LogoUrl { get; set; }
    public string? LeagueLogoUrl { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public string Venue { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string StatusDetail { get; set; } = null!;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string EventUrl { get; set; } = null!;
    public BoxScoreDto? BoxScore { get; set; }
}
