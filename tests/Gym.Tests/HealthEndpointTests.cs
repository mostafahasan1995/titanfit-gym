using System.Net;
using System.Text.Json;
using Gym.Client;

namespace Gym.Tests;

public sealed class HealthEndpointTests(ApiWithoutDatabaseFactory factory)
    : IClassFixture<ApiWithoutDatabaseFactory>
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

    [Theory]
    [InlineData("/api/members")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/products")]
    [InlineData("/api/staff")]
    [InlineData("/api/auth/me")]
    public async Task Api_endpoints_need_a_login(string url)
    {
        using var http = factory.CreateClient();

        using var response = await http.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_made_up_token_is_rejected()
    {
        using var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-real-token");

        using var response = await http.GetAsync("/api/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_lists_the_endpoints_in_development()
    {
        using var http = factory.CreateClient();

        using var response = await http.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name.TrimEnd('/')).ToList();
        Assert.Contains("/api/members", paths);
        Assert.Contains("/api/sales", paths);
        Assert.Contains("/api/auth/login", paths);
        Assert.Contains("/api/private-trainers/{id}/service", paths);
    }
}
