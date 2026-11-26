using System.Net;
using System.Text.Json;
using Gym.Client;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gym.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_ok_status_and_version_as_json()
    {
        using var http = factory.CreateClient();

        using var response = await http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        Assert.Matches(@"^\d+\.\d+\.\d+", json.RootElement.GetProperty("version").GetString() ?? "");
    }

    [Fact]
    public async Task Api_client_reads_health_from_the_server()
    {
        var api = new GymApiClient(factory.CreateClient());

        var health = await api.GetHealthAsync();

        Assert.Equal("ok", health.Status);
        Assert.Matches(@"^\d+\.\d+\.\d+", health.Version);
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        using var http = factory.CreateClient();

        using var response = await http.GetAsync("/no-such-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
