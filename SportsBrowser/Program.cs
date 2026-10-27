using System.Reflection;
using Microsoft.Extensions.Options;
using SportsBrowser.Models;
using SportsBrowser.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EspnSettings>(builder.Configuration.GetSection("Espn"));
builder.Services.AddHttpClient<IEspnScheduleClient, EspnScheduleClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<EspnSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.0");
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // Make browsers revalidate (cheap via ETag) so CSS/JS changes show up without a hard refresh.
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});

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

app.MapGet("/api/version", () =>
{
    var version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    // Strip the source-control hash suffix (e.g. "1.0.0+abc123").
    return Results.Ok(new { Version = version.Split('+')[0] });
});

app.MapFallbackToFile("index.html");

app.Run();
