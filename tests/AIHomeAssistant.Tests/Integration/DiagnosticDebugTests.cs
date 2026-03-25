using AIHomeAssistant.Tests.Helpers;

namespace AIHomeAssistant.Tests.Integration;

[Collection("Integration")]
public class DiagnosticDebugTests
{
    private readonly HttpClient _client;

    public DiagnosticDebugTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Debug_ShowCommandLog500Body()
    {
        var res = await _client.GetAsync("/api/diagnostics/command-log");
        var body = await res.Content.ReadAsStringAsync();
        // This will print the actual error detail if 500
        Assert.True(res.IsSuccessStatusCode, $"Status: {(int)res.StatusCode} Body: {body}");
    }

    [Fact]
    public async Task Debug_ShowAlerts500Body()
    {
        var res = await _client.GetAsync("/api/ha/alerts");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"Status: {(int)res.StatusCode} Body: {body}");
    }
}

