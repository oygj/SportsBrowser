# SportsBrowser

A small ASP.NET Core (.NET 10) web app for browsing Philadelphia pro sports games by date. It pulls schedules, live scores and box scores from ESPN's public site API and shows them on one page.

Teams tracked by default:

| Sport | Team                  | ESPN league path   |
|-------|-----------------------|--------------------|
| NFL   | Philadelphia Eagles   | `football/nfl`     |
| NBA   | Philadelphia 76ers    | `basketball/nba`   |
| MLB   | Philadelphia Phillies | `baseball/mlb`     |
| NHL   | Philadelphia Flyers   | `hockey/nhl`       |
| MLS   | Philadelphia Union    | `soccer/usa.1`     |

## Features

- Pick any date, or step a day back or forward with the arrow buttons; **Today** jumps back to the current date.
- Each game card shows the opponent, home/away, start time, venue and status (scheduled, in progress, final).
- Finished games show the final score and a win/loss result.
- Box scores are fetched for finished games when ESPN provides team stats (mainly basketball: FG, 3PT, FT, rebounds, assists, turnovers, steals, blocks, fouls).

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run

```bash
dotnet run --project SportsBrowser
```

Then open <http://localhost:5146>. To run with HTTPS as well (<https://localhost:7151>):

```bash
dotnet run --project SportsBrowser --launch-profile https
```

No API key is needed. ESPN's site API is public, but it is also undocumented and can change without notice.

## API

The backend serves the static frontend from `wwwroot/` and exposes two JSON endpoints:

| Endpoint | Description |
|----------|-------------|
| `GET /api/teams` | The configured leagues and teams. |
| `GET /api/games?date=YYYY-MM-DD` | Games for the tracked teams on the given date, sorted by start time. Without `date`, uses the current UTC date. |

Example:

```bash
curl "http://localhost:5146/api/games?date=2026-10-22"
```

## Configuration

Teams are set in the `Espn` section of [SportsBrowser/appsettings.json](SportsBrowser/appsettings.json):

```json
"Espn": {
  "BaseUrl": "https://site.api.espn.com/apis/site/v2/sports/",
  "Leagues": [
    {
      "Sport": "NBA",
      "LeaguePath": "basketball/nba",
      "DisplayName": "Philadelphia 76ers",
      "TeamNameKeywords": ["76ers", "Philadelphia 76ers", "Sixers"]
    }
  ]
}
```

- `LeaguePath` is the ESPN path segment used to build `{BaseUrl}{LeaguePath}/scoreboard?dates=yyyyMMdd`.
- `TeamNameKeywords` picks out the tracked team in each game. A game matches if any keyword appears (case-insensitive) in a competitor's display name, short name or team name. Choose keywords specific enough not to match other teams.

To follow a different city or add a team, edit or add entries here. There is one tracked team per league entry. Note that the page title and header text in `wwwroot/index.html` mention Philadelphia, so change those too.

## Project structure

```
SportsBrowser/
├── Program.cs                  # App setup, DI, minimal API endpoints
├── Services/
│   └── EspnScheduleClient.cs   # Calls ESPN scoreboard/summary endpoints, parses JSON
├── Models/
│   ├── EspnSettings.cs         # Bound from the "Espn" config section
│   ├── LeagueInfo.cs           # One tracked team/league
│   ├── GameEventDto.cs         # Game data returned by /api/games
│   └── BoxScoreDto.cs          # Team box-score stats
├── wwwroot/
│   ├── index.html              # Single-page UI
│   ├── app.js                  # Date navigation, fetching and rendering
│   └── styles.css
└── appsettings.json            # ESPN base URL and league/team config
```

## How it works

1. For the requested date, `EspnScheduleClient` calls the `scoreboard` endpoint once per configured league, one league after another.
2. Each event is kept only if one competitor matches the league's `TeamNameKeywords`. It is then mapped to a `GameEventDto`.
3. For games that have scores, it also calls the `summary` endpoint to get team box-score stats. If that call fails, the game is still returned, just without a box score.
4. The frontend renders the games and lets you move between dates.


## Build

To build a new release version follow these steps:
1. `dotnet publish .\SportsBrowser\SportsBrowser.csproj -c Release -o .\publish`
2. `scp -p -r .\publish\* oyvind@192.168.1.8:/home/oyvind/tmp-dir `