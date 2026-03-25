using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure.Data;
using AIHomeAssistant.Infrastructure.HomeAssistant;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AIHomeAssistant.Tests.Unit.HomeAssistant;

public class CommandRouterTests
{
    private readonly IHomeAssistantClient _haClient = Substitute.For<IHomeAssistantClient>();
    private readonly IShoppingListRepository _shoppingList = Substitute.For<IShoppingListRepository>();
    private readonly ILogger<CommandRouter> _logger = Substitute.For<ILogger<CommandRouter>>();

    private CommandRouter CreateSut() => new(_haClient, _shoppingList, _logger);

    [Fact]
    public async Task RouteAsync_LightTurnOn_CallsHaLightService()
    {
        var sut = CreateSut();
        _haClient.CallServiceAsync("light", "turn_on",
                Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult(true));

        var intent = new IntentResult(
            IntentType.Action, "light.living_room", "light.turn_on",
            new Dictionary<string, object?>());

        var result = await sut.RouteAsync(intent, CancellationToken.None);

        Assert.True(result.Success);
        await _haClient.Received(1).CallServiceAsync(
            "light", "turn_on", Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_ClimateSetTemperature_CallsHaClimateService()
    {
        var sut = CreateSut();
        _haClient.CallServiceAsync("climate", "set_temperature",
                Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult(true));

        var intent = new IntentResult(
            IntentType.Action, "climate.living_room", "climate.set_temperature",
            new Dictionary<string, object?> { ["temperature"] = 22.0 });

        var result = await sut.RouteAsync(intent, CancellationToken.None);

        Assert.True(result.Success);
        await _haClient.Received(1).CallServiceAsync(
            "climate", "set_temperature", Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_ShoppingListAdd_CallsRepository()
    {
        var sut = CreateSut();
        var intent = new IntentResult(
            IntentType.Action, string.Empty, "shopping_list.add",
            new Dictionary<string, object?> { ["item"] = "latte" });

        await sut.RouteAsync(intent, CancellationToken.None);

        await _shoppingList.Received(1).AddItemAsync("latte", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_UnknownAction_ReturnsFailure()
    {
        var sut = CreateSut();
        var intent = new IntentResult(
            IntentType.Action, "unknown.entity", "totally_unknown_no_dot",
            new Dictionary<string, object?>());

        var result = await sut.RouteAsync(intent, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("UNKNOWN_ACTION", result.Error?.Code);
    }

    [Fact]
    public async Task RouteAsync_HaClientFails_ReturnsFailure()
    {
        var sut = CreateSut();
        _haClient.CallServiceAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult(false,
                new PipelineError("HA_SERVICE_CALL_FAILED", "HA returned 503")));

        var intent = new IntentResult(
            IntentType.Action, "light.living_room", "light.turn_on",
            new Dictionary<string, object?>());

        var result = await sut.RouteAsync(intent, CancellationToken.None);

        Assert.False(result.Success);
    }
}
