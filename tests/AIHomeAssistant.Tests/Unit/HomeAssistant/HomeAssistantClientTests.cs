using System.Net;
using System.Text;
using System.Text.Json;
using AIHomeAssistant.Infrastructure.HomeAssistant;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AIHomeAssistant.Tests.Unit.HomeAssistant;

public class HomeAssistantClientTests
{
    private readonly ILogger<HomeAssistantClient> _logger = Substitute.For<ILogger<HomeAssistantClient>>();

    private HomeAssistantClient CreateSut(HttpResponseMessage response)
    {
        var handler = new StubHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.local:8123/") };
        return new HomeAssistantClient(httpClient, _logger);
    }

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    [Fact]
    public async Task HomeAssistantClient_GetStateAsync_WhenApiReturns200_ReturnsSuccessResult()
    {
        // Arrange
        var payload = new
        {
            entity_id = "light.living_room",
            state = "on",
            attributes = new Dictionary<string, object?> { ["brightness"] = 255 },
            last_updated = "2026-03-25T21:30:00+00:00"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(payload) };
        var sut = CreateSut(response);

        // Act
        var result = await sut.GetStateAsync("light.living_room");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("light.living_room", result.Value.EntityId);
        Assert.Equal("on", result.Value.State);
    }

    [Fact]
    public async Task HomeAssistantClient_GetStateAsync_WhenApiReturns503_ReturnsFailureWithHaUnavailableCode()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var sut = CreateSut(response);

        // Act
        var result = await sut.GetStateAsync("light.living_room");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal("HA_UNAVAILABLE", result.Error.Code);
    }

    [Fact]
    public async Task HomeAssistantClient_GetAllStatesAsync_DeserializesEntityList()
    {
        // Arrange
        var payload = new[]
        {
            new { entity_id = "light.living_room", state = "on", attributes = new Dictionary<string, object?>(), last_updated = "2026-03-25T21:30:00+00:00" },
            new { entity_id = "climate.living_room", state = "heat", attributes = new Dictionary<string, object?>(), last_updated = "2026-03-25T21:30:00+00:00" }
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(payload) };
        var sut = CreateSut(response);

        // Act
        var result = await sut.GetAllStatesAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, s => s.EntityId == "light.living_room");
        Assert.Contains(result.Value, s => s.EntityId == "climate.living_room");
    }

    [Fact]
    public async Task HomeAssistantClient_GetStateAsync_WhenNetworkError_ReturnsHaUnavailableError()
    {
        // Arrange — handler throws HttpRequestException
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.local:8123/") };
        var sut = new HomeAssistantClient(httpClient, _logger);

        // Act
        var result = await sut.GetStateAsync("light.living_room");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("HA_UNAVAILABLE", result.Error?.Code);
    }

    // Stub handler returns a fixed response
    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    // Handler that throws on send
    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
