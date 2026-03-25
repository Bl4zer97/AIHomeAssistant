using System.Threading.Channels;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHomeAssistant.Infrastructure.Audio;

/// <summary>
/// Stub IWakeWordDetector that blocks until cancellation but never emits a wake-word.
/// Replace this class with a real Picovoice Porcupine integration when credentials
/// and the NuGet package (Porcupine by Picovoice) are available.
///
/// Integration guide:
///   1. Add NuGet: dotnet add package Porcupine
///   2. Set AudioOptions.PorcupineAccessKey in appsettings.json (or secrets)
///   3. Replace the body of StartAsync with Porcupine SDK calls:
///        var porcupine = Porcupine.Builder().SetAccessKey(_options.PorcupineAccessKey).Build();
///        // Read PCM frames from ALSA, call porcupine.Process(frame), write to output on match
/// </summary>
public sealed class PorcupineWakeWordDetector : IWakeWordDetector
{
    private readonly AudioOptions _options;
    private readonly ILogger<PorcupineWakeWordDetector> _logger;

    public PorcupineWakeWordDetector(IOptions<AudioOptions> options, ILogger<PorcupineWakeWordDetector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(ChannelWriter<AudioSegment> output, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.PorcupineAccessKey))
            _logger.LogWarning("PorcupineAccessKey not configured — wake-word detection is disabled. " +
                               "Set Audio:PorcupineAccessKey in appsettings or secrets.");
        else
            _logger.LogInformation("Porcupine access key present but SDK not yet integrated. " +
                                   "Wake-word detection is dormant.");

        // Block until cancellation (no real detection in stub mode)
        return Task.Delay(Timeout.Infinite, ct);
    }

    public Task StopAsync() => Task.CompletedTask;

    public void Dispose() { }
}
