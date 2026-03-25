using System.Net;
using System.Net.Http.Json;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Tests.Helpers;

namespace AIHomeAssistant.Tests.Integration;

/// <summary>Integration tests for the HA alert controller — Stories 3.x.</summary>
[Collection("Integration")]
public class HAControllerTests
{
    private readonly HttpClient _client;

    public HAControllerTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetActiveAlerts_Returns200WithArray()
    {
        var res = await _client.GetAsync("/api/ha/alerts");
        res.EnsureSuccessStatusCode();
        var alerts = await res.Content.ReadFromJsonAsync<AlertState[]>();
        Assert.NotNull(alerts);
    }

    [Fact]
    public async Task CreateAlert_ValidSensorId_Returns201WithAlertState()
    {
        var res = await _client.PostAsJsonAsync(
            "/api/ha/alerts",
            new { SensorId = "sensor.front_door" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var alert = await res.Content.ReadFromJsonAsync<AlertState>();
        Assert.NotNull(alert);
        Assert.Equal("sensor.front_door", alert!.SensorId);
        Assert.Equal("active", alert.Status);
    }

    [Fact]
    public async Task GetAlert_AfterCreate_Returns200()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/ha/alerts",
            new { SensorId = "sensor.window_1" });
        var created = await create.Content.ReadFromJsonAsync<AlertState>();
        Assert.NotNull(created);

        var get = await _client.GetAsync($"/api/ha/alerts/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task GetAlert_NonExistent_Returns404()
    {
        var res = await _client.GetAsync("/api/ha/alerts/99999");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task AcknowledgeAlert_ValidId_Returns204()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/ha/alerts",
            new { SensorId = "sensor.motion_1" });
        var created = await create.Content.ReadFromJsonAsync<AlertState>();

        var ack = await _client.PostAsync($"/api/ha/alerts/{created!.Id}/acknowledge", null);
        Assert.Equal(HttpStatusCode.NoContent, ack.StatusCode);
    }

    [Fact]
    public async Task ClearAlert_ValidId_Returns204()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/ha/alerts",
            new { SensorId = "sensor.smoke_1" });
        var created = await create.Content.ReadFromJsonAsync<AlertState>();

        var clear = await _client.PostAsync($"/api/ha/alerts/{created!.Id}/clear", null);
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
    }
}
