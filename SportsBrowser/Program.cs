using Microsoft.Extensions.Options;
using SportsBrowser.Models;
using SportsBrowser.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EspnSettings>(builder.Configuration.GetSection("Espn"));
builder.Services.AddHttpClient<IEspnScheduleClient, EspnScheduleClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<EspnSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/teams", (IEspnScheduleClient scheduleClient) =>
{
    var teams = scheduleClient.SupportedLeagues.Select(league => new
    {
        league.Sport,
        league.DisplayName,
        league.LeaguePath,
        league.TeamNameKeywords
    });

    return Results.Ok(teams);
});

app.MapGet("/api/games", async (DateTime? date, IEspnScheduleClient scheduleClient) =>
{
    var targetDate = date?.Date ?? DateTime.UtcNow.Date;
    var games = await scheduleClient.GetGamesForDateAsync(targetDate);
    return Results.Ok(games);
});

app.MapFallbackToFile("index.html");

app.Run();
