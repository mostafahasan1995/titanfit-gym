using Gym.Api;
using Gym.Api.Auth;
using Gym.Api.Endpoints;
using Gym.Api.Live;
using Gym.Infrastructure.Storage;
using Microsoft.Extensions.Hosting.WindowsServices;

// Configuration (appsettings.json, then environment variables such as ConnectionStrings__Gym):
//   ConnectionStrings:Gym        PostgreSQL connection string (never commit one with a password)
//   Database:MigrateOnStartup    apply migrations at start (default true)
//   Storage:DataPath             photos and the token key (default %ProgramData%\TitanFit)
//   Jwt:Key                      optional signing key (32+ bytes); by default one is generated in Storage:DataPath/keys
//   Jwt:LifetimeHours            login lifetime (default 12)
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});

builder.Services.AddWindowsService(options => options.ServiceName = "TitanFit API");
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddGymInfrastructure(services => services.GetRequiredService<IConfiguration>().GetConnectionString("Gym"));
builder.Services.ConfigureHttpJsonOptions(options => GymJson.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GymExceptionHandler>();
builder.Services.AddGymAuth();
builder.Services.AddSignalR().AddJsonProtocol(options => GymJson.Configure(options.PayloadSerializerOptions));
builder.Services.AddSingleton<LiveUpdates>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "TitanFit API"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = AppVersion.Current })).AllowAnonymous();
app.MapGymApi();
app.MapHub<GymHub>(GymHubRoute.Path).RequireAuthorization(Policies.AnyStaff);

await app.InitializeDatabaseAsync();
await app.RunAsync();

/// <summary>Entry point. Public so the tests can host the API with WebApplicationFactory.</summary>
public partial class Program { }
