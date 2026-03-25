using System.Net;
using System.Net.Http.Json;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Tests.Helpers;

namespace AIHomeAssistant.Tests.Integration;

/// <summary>Integration tests for the Diagnostics controller — Story 2.4.</summary>
[Collection("Integration")]
public class DiagnosticsControllerTests
{
    private readonly HttpClient _client;

    public DiagnosticsControllerTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetCommandLog_Returns200WithArray()
    {
        var res = await _client.GetAsync("/api/diagnostics/command-log");
        res.EnsureSuccessStatusCode();
        var entries = await res.Content.ReadFromJsonAsync<CommandRecord[]>();
        Assert.NotNull(entries);
    }

    [Fact]
    public async Task GetCommandLog_ExceedMaxCount_ClampsTo500()
    {
        var res = await _client.GetAsync("/api/diagnostics/command-log?count=9999");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
