using Gym.Api;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = AppVersion.Current }));

app.Run();

/// <summary>Entry point. Public so the tests can host the API with WebApplicationFactory.</summary>
public partial class Program { }
