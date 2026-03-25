using System.Diagnostics;
using System.Threading.Channels;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHomeAssistant.Infrastructure.Pipeline;

/// <summary>
/// Orchestrates the end-to-end voice pipeline:
/// AudioSegment -> STT -> IntentResolution -> CommandRouter -> AudioFeedback -> CommandLog.
/// Uses IServiceScopeFactory for Scoped deps (ICommandRouter, ICommandRepository).
/// </summary>
public sealed class VoicePipelineOrchestrator : IHostedService
{
    private readonly Channel<AudioSegment> _audioChannel;
    private readonly ISpeechToTextService _stt;
    private readonly IIntentResolutionService _intent;
    private readonly IHaStateCacheService _haCache;
    private readonly IAudioFeedbackService _audio;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioOptions _audioOptions;
    private readonly ILogger<VoicePipelineOrchestrator> _logger;
    private readonly List<string> _sessionHistory = [];
    private CancellationTokenSource? _cts;
    private Task? _pipelineTask;

    public VoicePipelineOrchestrator(
        Channel<AudioSegment> audioChannel,
        ISpeechToTextService stt,
        IIntentResolutionService intent,
        IHaStateCacheService haCache,
        IAudioFeedbackService audio,
        IServiceScopeFactory scopeFactory,
        IOptions<AudioOptions> audioOptions,
        ILogger<VoicePipelineOrchestrator> logger)
    {
        _audioChannel = audioChannel;
        _stt = stt;
        _intent = intent;
        _haCache = haCache;
        _audio = audio;
        _scopeFactory = scopeFactory;
        _audioOptions = audioOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pipelineTask = RunPipelineLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) await _cts.CancelAsync();
        if (_pipelineTask is not null)
        {
            try { await _pipelineTask.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { }
        }
        _cts?.Dispose();
    }

    private async Task RunPipelineLoopAsync(CancellationToken ct)
    {
        await foreach (var segment in _audioChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessSegmentAsync(segment, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected pipeline error -- resetting and continuing");
                await TryPlayErrorAsync(ct);
            }
        }
    }

    private async Task ProcessSegmentAsync(AudioSegment segment, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        string? transcript = null;
        string? resolvedIntentAction = null;
        string? entityId = null;
        int? haResponseCode = null;
        string? errorCode = null;

        // STT
        var sttResult = await _stt.TranscribeAsync(segment, ct);
        if (!sttResult.Success)
        {
            errorCode = sttResult.Error?.Code;
            _logger.LogWarning("STT failed: {ErrorCode}", errorCode);
            await TryPlayErrorAsync(ct);
            await PersistCommandAsync(transcript, resolvedIntentAction, entityId,
                haResponseCode, (int)sw.ElapsedMilliseconds, errorCode, ct);
            return;
        }

        transcript = sttResult.Value;
        _logger.LogInformation("Transcript: {Transcript}", transcript);

        // Intent Resolution
        var haContext = _haCache.GetAllStates();
        var intentResult = await _intent.ResolveIntentAsync(transcript!, haContext, _sessionHistory, ct);
        if (!intentResult.Success)
        {
            errorCode = intentResult.Error?.Code;
            _logger.LogWarning("Intent resolution failed: {ErrorCode}", errorCode);
            await TryPlayErrorAsync(ct);
            await PersistCommandAsync(transcript, resolvedIntentAction, entityId,
                haResponseCode, (int)sw.ElapsedMilliseconds, errorCode, ct);
            return;
        }

        var intent = intentResult.Value!;
        resolvedIntentAction = intent.Action;
        entityId = intent.EntityId;

        // Command Routing (Scoped)
        using var scope = _scopeFactory.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<ICommandRouter>();
        var routeResult = await router.RouteAsync(intent, ct);
        if (!routeResult.Success)
        {
            errorCode = routeResult.Error?.Code;
            haResponseCode = 503;
            _logger.LogWarning("Command routing failed: {ErrorCode}", errorCode);
            await TryPlayErrorAsync(ct);
            await PersistCommandAsync(transcript, resolvedIntentAction, entityId,
                haResponseCode, (int)sw.ElapsedMilliseconds, errorCode, ct);
            return;
        }

        haResponseCode = 200;
        _sessionHistory.Add(transcript!);
        if (_sessionHistory.Count > 10) _sessionHistory.RemoveAt(0);

        // Audio Feedback
        if (intent.IntentType == IntentType.Query && routeResult.Value is { } response)
            await _audio.PlaySpeechAsync(response, ct);
        else
            await _audio.PlaySuccessAsync(ct);

        var commandRepo = scope.ServiceProvider.GetRequiredService<ICommandRepository>();
        await PersistCommandAsync(transcript, resolvedIntentAction, entityId,
            haResponseCode, (int)sw.ElapsedMilliseconds, null, ct, commandRepo);

        _logger.LogInformation("Pipeline complete: {Action} -> {EntityId} in {LatencyMs}ms",
            resolvedIntentAction, entityId, sw.ElapsedMilliseconds);
    }

    private async Task PersistCommandAsync(
        string? transcript, string? resolvedIntent, string? entityId,
        int? haResponseCode, int latencyMs, string? errorCode,
        CancellationToken ct, ICommandRepository? repo = null)
    {
        ICommandRepository? commandLog = repo;
        IServiceScope? ownedScope = null;
        try
        {
            if (commandLog is null)
            {
                ownedScope = _scopeFactory.CreateScope();
                commandLog = ownedScope.ServiceProvider.GetRequiredService<ICommandRepository>();
            }
            await commandLog.InsertAsync(new CommandRecord(
                0, DateTimeOffset.UtcNow, transcript, resolvedIntent, entityId,
                haResponseCode, latencyMs, _audioOptions.FeedbackProfile, errorCode), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write command log entry");
        }
        finally
        {
            ownedScope?.Dispose();
        }
    }

    private async Task TryPlayErrorAsync(CancellationToken ct)
    {
        try { await _audio.PlayErrorAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not play error feedback"); }
    }
}