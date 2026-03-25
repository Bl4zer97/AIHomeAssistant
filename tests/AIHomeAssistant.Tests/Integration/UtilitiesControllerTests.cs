using System.Net;
using System.Net.Http.Json;
using AIHomeAssistant.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace AIHomeAssistant.Tests.Integration;

/// <summary>Integration tests for the Utilities controller (shopping list) — Story 2.2.</summary>
[Collection("Integration")]
public class UtilitiesControllerTests
{
    private readonly HttpClient _client;

    public UtilitiesControllerTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetShoppingList_WhenEmpty_Returns200WithEmptyArray()
    {
        var res = await _client.GetAsync("/api/utilities/shopping-list");
        res.EnsureSuccessStatusCode();
        var items = await res.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(items);
    }

    [Fact]
    public async Task AddShoppingItem_ValidItem_Returns201()
    {
        var res = await _client.PostAsJsonAsync(
            "/api/utilities/shopping-list",
            new { Item = "pane" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task AddShoppingItem_EmptyItem_Returns422()
    {
        var res = await _client.PostAsJsonAsync(
            "/api/utilities/shopping-list",
            new { Item = string.Empty });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task ClearShoppingList_Returns204()
    {
        var res = await _client.DeleteAsync("/api/utilities/shopping-list");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task AddThenGetShoppingItem_RoundTrip_ItemAppearsInList()
    {
        await _client.PostAsJsonAsync("/api/utilities/shopping-list", new { Item = "latte" });

        var res = await _client.GetAsync("/api/utilities/shopping-list");
        res.EnsureSuccessStatusCode();
        var items = await res.Content.ReadFromJsonAsync<string[]>();
        Assert.Contains("latte", items!);
    }
}
