using Microsoft.Extensions.Hosting;
using System.Threading.Channels;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIHomeAssistant.Infrastructure.Options;

namespace AIHomeAssistant.Infrastructure.Audio;

/// <summary>
/// IHostedService that listens for wake-word using an IWakeWordDetector and
/// feeds captured audio into the pipeline channel.
/// Implements IHostedService directly (not BackgroundService) per architecture convention.
/// </summary>
public class WakeWordHostedService : IHostedService
{
    private readonly IWakeWordDetector _detector;
    private readonly IAudioCaptureService _capture;
    private readonly Channel<AudioSegment> _pipeline;
    private readonly ILogger<WakeWordHostedService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public WakeWordHostedService(
        IWakeWordDetector detector,
        IAudioCaptureService capture,
        Channel<AudioSegment> pipeline,
        ILogger<WakeWordHostedService> logger)
    {
        _detector = detector;
        _capture = capture;
        _pipeline = pipeline;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = RunListenLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        if (_listenTask is not null)
        {
            try { await _listenTask; }
            catch (OperationCanceledException) { }
        }

        await _detector.StopAsync();
        _cts?.Dispose();
    }

    private async Task RunListenLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Wake-word listener starting");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _detector.StartAsync(_pipeline.Writer, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wake-word detector error — retrying in 5s");
                try { await Task.Delay(5000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        _pipeline.Writer.TryComplete();
        _logger.LogInformation("Wake-word listener stopped");
    }
}
