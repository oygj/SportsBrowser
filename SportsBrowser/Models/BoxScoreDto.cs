namespace SportsBrowser.Models;

public sealed class BoxScoreDto
{
    public TeamBoxScoreDto? HomeTeam { get; set; }
    public TeamBoxScoreDto? AwayTeam { get; set; }
}

public sealed class TeamBoxScoreDto
{
    public string TeamName { get; set; } = null!;
    public int? FieldGoalsMade { get; set; }
    public int? FieldGoalsAttempted { get; set; }
    public int? ThreePointersMade { get; set; }
    public int? ThreePointersAttempted { get; set; }
    public int? FreeThrowsMade { get; set; }
    public int? FreeThrowsAttempted { get; set; }
    public int? Rebounds { get; set; }
    public int? Assists { get; set; }
    public int? Turnovers { get; set; }
    public int? Steals { get; set; }
    public int? Blocks { get; set; }
    public int? Fouls { get; set; }
}
