using System.Text.Json;
using Microsoft.Extensions.Options;
using SportsBrowser.Models;

namespace SportsBrowser.Services;

public interface IEspnScheduleClient
{
    Task<IEnumerable<GameEventDto>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    IReadOnlyCollection<LeagueInfo> SupportedLeagues { get; }
}

public sealed class EspnScheduleClient : IEspnScheduleClient
{
    private readonly HttpClient _httpClient;
    private readonly EspnSettings _settings;

    public EspnScheduleClient(HttpClient httpClient, IOptions<EspnSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public IReadOnlyCollection<LeagueInfo> SupportedLeagues => _settings.Leagues;

    public async Task<IEnumerable<GameEventDto>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var requests = _settings.Leagues.Select(league => FetchLeagueGamesAsync(league, date, cancellationToken));
        var results = await Task.WhenAll(requests);
        return results.SelectMany(x => x).OrderBy(x => x.StartDate).ToArray();
    }

    private async Task<IEnumerable<GameEventDto>> FetchLeagueGamesAsync(LeagueInfo league, DateTime date, CancellationToken cancellationToken)
    {
        var url = $"{league.LeaguePath}/scoreboard?dates={date:yyyyMMdd}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("events", out var eventsElement) || eventsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<GameEventDto>();
        }

        var matches = new List<GameEventDto>();
        foreach (var eventElement in eventsElement.EnumerateArray())
        {
            if (!TryCreateMatch(league, eventElement, out var match))
            {
                continue;
            }

            // Fetch box score for finished games
            if (match!.HomeScore.HasValue && match.AwayScore.HasValue)
            {
                match.BoxScore = await FetchBoxScoreAsync(league, match.GameId, cancellationToken);
            }

            matches.Add(match);
        }

        return matches;
    }

    private bool TryCreateMatch(LeagueInfo league, JsonElement eventElement, out GameEventDto? match)
    {
        match = null;

        if (!eventElement.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var gameId = idElement.GetString() ?? string.Empty;
        var startDate = eventElement.TryGetProperty("date", out var dateElement) && dateElement.ValueKind == JsonValueKind.String
            ? DateTimeOffset.Parse(dateElement.GetString()!, null, System.Globalization.DateTimeStyles.AssumeUniversal)
            : DateTimeOffset.MinValue;

        var status = string.Empty;
        var statusDetail = string.Empty;
        if (eventElement.TryGetProperty("status", out var statusElement) && statusElement.TryGetProperty("type", out var statusTypeElement))
        {
            status = statusTypeElement.GetProperty("name").GetString() ?? string.Empty;
            statusDetail = statusTypeElement.GetProperty("detail").GetString() ?? string.Empty;
        }

        var eventUrl = string.Empty;
        if (eventElement.TryGetProperty("links", out var linksElement) && linksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linksElement.EnumerateArray())
            {
                if (link.TryGetProperty("href", out var hrefElement) && hrefElement.ValueKind == JsonValueKind.String)
                {
                    eventUrl = hrefElement.GetString() ?? eventUrl;
                }
            }
        }

        if (!eventElement.TryGetProperty("competitions", out var competitionsElement) || competitionsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var competition = competitionsElement[0];
        if (!competition.TryGetProperty("competitors", out var competitorsElement) || competitorsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        JsonElement? philadelphiaCompetitor = null;
        JsonElement? opponentCompetitor = null;
        foreach (var competitor in competitorsElement.EnumerateArray())
        {
            if (!competitor.TryGetProperty("team", out var teamElement))
            {
                continue;
            }

            var displayName = teamElement.GetProperty("displayName").GetString() ?? string.Empty;
            var shortDisplayName = teamElement.GetProperty("shortDisplayName").GetString() ?? string.Empty;
            var name = teamElement.GetProperty("name").GetString() ?? string.Empty;

            if (league.TeamNameKeywords.Any(keyword => ContainsToken(displayName, keyword) || ContainsToken(shortDisplayName, keyword) || ContainsToken(name, keyword)))
            {
                philadelphiaCompetitor = competitor;
                continue;
            }

            opponentCompetitor = competitor;
        }

        if (philadelphiaCompetitor is null || opponentCompetitor is null)
        {
            return false;
        }

        var venue = competition.TryGetProperty("venue", out var venueElement) && venueElement.TryGetProperty("fullName", out var venueName)
            ? venueName.GetString() ?? string.Empty
            : string.Empty;

        var homeScore = GetScore(competitorsElement, "home");
        var awayScore = GetScore(competitorsElement, "away");

        var philadelphiaTeamName = GetTeamName(philadelphiaCompetitor.Value);
        var philadelphiaLogoUrl = GetTeamLogoUrl(philadelphiaCompetitor.Value);
        var opponentTeamName = GetTeamName(opponentCompetitor.Value);
        var homeTeamName = GetTeamNameByHomeAway(competitorsElement, "home");
        var awayTeamName = GetTeamNameByHomeAway(competitorsElement, "away");
        var homeAbbr = GetTeamAbbreviationByHomeAway(competitorsElement, "home");
        var awayAbbr = GetTeamAbbreviationByHomeAway(competitorsElement, "away");
        var isHome = philadelphiaCompetitor.Value.GetProperty("homeAway").GetString() == "home";

        match = new GameEventDto
        {
            GameId = gameId,
            Sport = league.Sport,
            LeaguePath = league.LeaguePath,
            TeamDisplayName = philadelphiaTeamName,
            OpponentDisplayName = opponentTeamName,
            IsHome = isHome,
            HomeTeam = homeTeamName,
            AwayTeam = awayTeamName,
            HomeTeamAbbreviation = homeAbbr,
            AwayTeamAbbreviation = awayAbbr,
            StartDate = startDate,
            Venue = venue,
            Status = status,
            StatusDetail = statusDetail,
            HomeScore = homeScore,
            AwayScore = awayScore,
            EventUrl = eventUrl,
            LogoUrl = philadelphiaLogoUrl,
        };

        return true;
    }

    private static string GetTeamLogoUrl(JsonElement competitor)
    {
        if (!competitor.TryGetProperty("team", out var teamElement))
        {
            return string.Empty;
        }

        string ExtractHref(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("href", out var hrefElement) && hrefElement.ValueKind == JsonValueKind.String)
                {
                    return hrefElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String)
                {
                    return urlElement.GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        if (teamElement.TryGetProperty("logo", out var logoElement))
        {
            var url = ExtractHref(logoElement);
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }
        }

        if (teamElement.TryGetProperty("logos", out var logosElement) && logosElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var logo in logosElement.EnumerateArray())
            {
                var url = ExtractHref(logo);
                if (!string.IsNullOrEmpty(url))
                {
                    return url;
                }
            }
        }

        if (teamElement.TryGetProperty("icon", out var iconElement))
        {
            var url = ExtractHref(iconElement);
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }
        }

        return string.Empty;
    }

    private static bool ContainsToken(string value, string token)
        => value.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static string GetTeamName(JsonElement competitor)
    {
        if (competitor.TryGetProperty("team", out var teamElement) && teamElement.TryGetProperty("displayName", out var displayNameElement))
        {
            return displayNameElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static int? GetScore(JsonElement competitorArray, string homeAway)
    {
        foreach (var competitor in competitorArray.EnumerateArray())
        {
            if (competitor.GetProperty("homeAway").GetString() == homeAway && competitor.TryGetProperty("score", out var scoreElement))
            {
                if (scoreElement.ValueKind == JsonValueKind.String && int.TryParse(scoreElement.GetString(), out var score))
                {
                    return score;
                }

                if (scoreElement.ValueKind == JsonValueKind.Number && scoreElement.TryGetInt32(out score))
                {
                    return score;
                }
            }
        }

        return null;
    }

    private static string GetTeamNameByHomeAway(JsonElement competitorArray, string homeAway)
    {
        foreach (var competitor in competitorArray.EnumerateArray())
        {
            if (competitor.GetProperty("homeAway").GetString() == homeAway)
            {
                return GetTeamName(competitor);
            }
        }

        return string.Empty;
    }

    private static string GetTeamAbbreviationByHomeAway(JsonElement competitorArray, string homeAway)
    {
        foreach (var competitor in competitorArray.EnumerateArray())
        {
            if (competitor.GetProperty("homeAway").GetString() == homeAway && competitor.TryGetProperty("team", out var teamElement) && teamElement.TryGetProperty("abbreviation", out var abbrElement))
            {
                return abbrElement.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private async Task<BoxScoreDto?> FetchBoxScoreAsync(LeagueInfo league, string gameId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{league.LeaguePath}/summary?id={gameId}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            
            // Look for competitors with stats (most common structure)
            if (document.RootElement.TryGetProperty("competitions", out var competitionsEl) && 
                competitionsEl.ValueKind == JsonValueKind.Array)
            {
                var competitions = competitionsEl.EnumerateArray().ToList();
                if (competitions.Count > 0 && competitions[0].TryGetProperty("competitors", out var competitorsEl))
                {
                    var result = ParseBoxScoreFromCompetitors(competitorsEl);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            // Try boxscore structure
            if (document.RootElement.TryGetProperty("boxscore", out var boxscoreElement))
            {
                if (boxscoreElement.TryGetProperty("teams", out var teamsElement) && teamsElement.ValueKind == JsonValueKind.Array)
                {
                    var boxScore = new BoxScoreDto();
                    var teamsList = teamsElement.EnumerateArray().ToList();
                    
                    if (teamsList.Count >= 2)
                    {
                        boxScore.AwayTeam = ExtractTeamStats(teamsList[0]);
                        boxScore.HomeTeam = ExtractTeamStats(teamsList[1]);
                        return boxScore;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static BoxScoreDto? ParseBoxScoreFromCompetitors(JsonElement competitorsEl)
    {
        var competitors = competitorsEl.EnumerateArray().ToList();
        if (competitors.Count < 2)
        {
            return null;
        }

        var away = ExtractTeamStatsFromCompetitor(competitors[0]);
        var home = ExtractTeamStatsFromCompetitor(competitors[1]);

        if (away == null || home == null)
        {
            return null;
        }

        // Only return box score if we have actual stats
        if ((away.FieldGoalsMade.HasValue || away.Assists.HasValue) && 
            (home.FieldGoalsMade.HasValue || home.Assists.HasValue))
        {
            return new BoxScoreDto { AwayTeam = away, HomeTeam = home };
        }

        return null;
    }

    private static TeamBoxScoreDto? ExtractTeamStatsFromCompetitor(JsonElement competitor)
    {
        var stats = new TeamBoxScoreDto
        {
            TeamName = competitor.TryGetProperty("team", out var teamObj) && teamObj.TryGetProperty("displayName", out var nameEl)
                ? nameEl.GetString() ?? string.Empty
                : string.Empty
        };

        if (!competitor.TryGetProperty("statistics", out var statsElement) || statsElement.ValueKind != JsonValueKind.Array)
        {
            return stats;
        }

        var statsArray = statsElement.EnumerateArray().ToList();
        if (statsArray.Count == 0)
        {
            return stats;
        }

        var statsObj = statsArray[0];
        if (!statsObj.TryGetProperty("stats", out var statsList) || statsList.ValueKind != JsonValueKind.Array)
        {
            return stats;
        }

        ParseStats(statsList, stats);
        return stats;
    }

    private static void ParseStats(JsonElement statsList, TeamBoxScoreDto stats)
    {
        foreach (var stat in statsList.EnumerateArray())
        {
            if (!stat.TryGetProperty("label", out var labelEl) || !stat.TryGetProperty("value", out var valueEl))
            {
                continue;
            }

            var label = labelEl.GetString() ?? string.Empty;
            var value = valueEl.ValueKind == JsonValueKind.Number ? (int?)valueEl.GetInt32() : null;

            switch (label.ToLower())
            {
                case "fieldgoalsattempted":
                    stats.FieldGoalsAttempted = value;
                    break;
                case "fieldgoalsmade":
                    stats.FieldGoalsMade = value;
                    break;
                case "threepointersmade":
                    stats.ThreePointersMade = value;
                    break;
                case "threepointersattempted":
                    stats.ThreePointersAttempted = value;
                    break;
                case "freethrowsmade":
                    stats.FreeThrowsMade = value;
                    break;
                case "freethrowsattempted":
                    stats.FreeThrowsAttempted = value;
                    break;
                case "totalrebounds":
                    stats.Rebounds = value;
                    break;
                case "assists":
                    stats.Assists = value;
                    break;
                case "turnovers":
                    stats.Turnovers = value;
                    break;
                case "steals":
                    stats.Steals = value;
                    break;
                case "blockedshots":
                    stats.Blocks = value;
                    break;
                case "fouls":
                    stats.Fouls = value;
                    break;
            }
        }
    }

    private static TeamBoxScoreDto ExtractTeamStats(JsonElement teamElement)
    {
        var stats = new TeamBoxScoreDto
        {
            TeamName = teamElement.TryGetProperty("team", out var teamObj) && teamObj.TryGetProperty("displayName", out var nameEl)
                ? nameEl.GetString() ?? string.Empty
                : string.Empty
        };

        if (!teamElement.TryGetProperty("statistics", out var statsElement) || statsElement.ValueKind != JsonValueKind.Array)
        {
            return stats;
        }

        var statsArray = statsElement.EnumerateArray().ToList();
        if (statsArray.Count == 0)
        {
            return stats;
        }

        var statsObj = statsArray[0];
        if (!statsObj.TryGetProperty("stats", out var statsList) || statsList.ValueKind != JsonValueKind.Array)
        {
            return stats;
        }

        ParseStats(statsList, stats);
        return stats;
    }
}

