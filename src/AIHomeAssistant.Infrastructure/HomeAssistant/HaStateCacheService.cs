using System.Collections.Concurrent;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIHomeAssistant.Infrastructure.HomeAssistant;

public class HaStateCacheService : IHaStateCacheService, IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HaStateCacheService> _logger;
    private readonly ConcurrentDictionary<string, HaState> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastSuccessfulPollAt;
    private bool _loggedNotConfigured;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(60);

    public HaStateCacheService(IServiceScopeFactory scopeFactory, ILogger<HaStateCacheService> logger)
    {
        _scopeFactory = scopeFactory;
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
        using var scope = _scopeFactory.CreateScope();
        var haClient = scope.ServiceProvider.GetRequiredService<IHomeAssistantClient>();
        var result = await haClient.GetAllStatesAsync(ct);

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
            if (_lastSuccessfulPollAt is null)
            {
                if (!_loggedNotConfigured)
                {
                    _loggedNotConfigured = true;
                    _logger.LogWarning(
                        "HA poll failed (never connected). Error: {ErrorCode} — {ErrorMessage}. Further failures will be logged at Debug.",
                        result.Error?.Code, result.Error?.Message);
                }
                else
                {
                    _logger.LogDebug(
                        "HA poll still failing (never connected). Error: {ErrorCode}",
                        result.Error?.Code);
                }
            }
            else
            {
                var staleDuration = DateTimeOffset.UtcNow - _lastSuccessfulPollAt.Value;

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
}
