using AIHomeAssistant.Tests.Helpers;

namespace AIHomeAssistant.Tests.Integration;

[Collection("Integration")]
public class HealthEndpointTests
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_Get_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
