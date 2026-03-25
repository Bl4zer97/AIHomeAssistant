using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure.HomeAssistant;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AIHomeAssistant.Core.Interfaces;

namespace AIHomeAssistant.Tests.Unit.HomeAssistant;

public class HaStateCacheServiceTests
{
    private readonly IHomeAssistantClient _mockClient = Substitute.For<IHomeAssistantClient>();
    private readonly ILogger<HaStateCacheService> _logger = Substitute.For<ILogger<HaStateCacheService>>();

    private HaStateCacheService CreateSut() => new(_mockClient, _logger);

    private static HaState MakeState(string entityId, string state = "on") =>
        new(entityId, state, new Dictionary<string, object?>(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task HaStateCacheService_StartAsync_PopulatesCache_OnFirstPoll()
    {
        // Arrange
        var states = new List<HaState>
        {
            MakeState("light.living_room"),
            MakeState("climate.living_room", "heat")
        };
        _mockClient.GetAllStatesAsync(Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<IReadOnlyList<HaState>>(true, states));

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(100); // Allow first poll to complete
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal("on", sut.GetState("light.living_room")?.State);
        Assert.Equal("heat", sut.GetState("climate.living_room")?.State);
    }

    [Fact]
    public async Task HaStateCacheService_GetState_WhenHaUnavailable_ReturnsLastKnownState()
    {
        // Arrange — first poll succeeds, subsequent polls fail
        var states = new List<HaState> { MakeState("light.living_room") };
        var successResult = new PipelineResult<IReadOnlyList<HaState>>(true, states);
        var failResult = new PipelineResult<IReadOnlyList<HaState>>(false,
            Error: new PipelineError("HA_UNAVAILABLE", "Connection refused"));

        _mockClient.GetAllStatesAsync(Arg.Any<CancellationToken>())
            .Returns(successResult, failResult);

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        // Act — start so first poll populates cache
        await sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await sut.StopAsync(CancellationToken.None);

        // Assert — state from first successful poll is retained
        var cached = sut.GetState("light.living_room");
        Assert.NotNull(cached);
        Assert.Equal("on", cached.State);
    }

    [Fact]
    public async Task HaStateCacheService_GetState_WhenEntityNotCached_ReturnsNull()
    {
        // Arrange
        _mockClient.GetAllStatesAsync(Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<IReadOnlyList<HaState>>(true, new List<HaState>()));

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await sut.StopAsync(CancellationToken.None);

        // Act
        var result = sut.GetState("sensor.unknown_entity");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HaStateCacheService_GetAllStates_ReturnsAllCachedStates()
    {
        // Arrange
        var states = new List<HaState>
        {
            MakeState("light.living_room"),
            MakeState("light.kitchen"),
            MakeState("climate.living_room", "heat")
        };
        _mockClient.GetAllStatesAsync(Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<IReadOnlyList<HaState>>(true, states));

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await sut.StopAsync(CancellationToken.None);

        // Act
        var all = sut.GetAllStates();

        // Assert
        Assert.Equal(3, all.Count);
    }
}
