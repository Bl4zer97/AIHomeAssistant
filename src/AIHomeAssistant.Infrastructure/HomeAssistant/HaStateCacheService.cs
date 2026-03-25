using System.Collections.Concurrent;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIHomeAssistant.Infrastructure.HomeAssistant;

public class HaStateCacheService : IHaStateCacheService, IHostedService
{
    private readonly IHomeAssistantClient _haClient;
    private readonly ILogger<HaStateCacheService> _logger;
    private readonly ConcurrentDictionary<string, HaState> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _lastSuccessfulPollAt = DateTimeOffset.MinValue;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(60);

    public HaStateCacheService(IHomeAssistantClient haClient, ILogger<HaStateCacheService> logger)
    {
        _haClient = haClient;
        _logger = logger;
    }

    public HaState? GetState(string entityId)
    {
        _cache.TryGetValue(entityId, out var state);
        return state;
    }

    public IReadOnlyList<HaState> GetAllStates() =>
        _cache.Values.ToList().AsReadOnly();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start background polling loop (first poll happens immediately inside)
        _pollTask = RunPollLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_pollTask is not null)
        {
            try
            {
                await _pollTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        _cts?.Dispose();
    }

    private async Task RunPollLoopAsync(CancellationToken ct)
    {
        // Immediate first poll — do not wait 30s for first data
        await PollOnceAsync(ct);

        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await PollOnceAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var result = await _haClient.GetAllStatesAsync(ct);

        if (result.Success && result.Value is not null)
        {
            foreach (var state in result.Value)
            {
                _cache[state.EntityId] = state;
            }

            _lastSuccessfulPollAt = DateTimeOffset.UtcNow;
            _logger.LogDebug("HA state cache refreshed: {Count} entities", result.Value.Count);
        }
        else
        {
            var staleDuration = DateTimeOffset.UtcNow - _lastSuccessfulPollAt;

            if (staleDuration > StaleThreshold)
            {
                _logger.LogError(
                    "HA state cache is stale: last successful poll was {StaleDuration:g} ago. Error: {ErrorCode}",
                    staleDuration, result.Error?.Code);
            }
            else
            {
                _logger.LogWarning(
                    "HA poll failed, retaining last-known cache. Error: {ErrorCode} — {ErrorMessage}",
                    result.Error?.Code, result.Error?.Message);
            }
        }
    }
}
