using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;

namespace AIHomeAssistant.Infrastructure.Audio;

/// <summary>
/// Plays WAV tone files (success.wav / error.wav) for audio feedback,
/// and delegates TTS playback to ITextToSpeechService.
/// </summary>
public class AudioFeedbackService : IAudioFeedbackService
{
    private readonly AudioOptions _options;
    private readonly ITextToSpeechService _tts;
    private readonly ILogger<AudioFeedbackService> _logger;

    public AudioFeedbackService(
        IOptions<AudioOptions> options,
        ITextToSpeechService tts,
        ILogger<AudioFeedbackService> logger)
    {
        _options = options.Value;
        _tts = tts;
        _logger = logger;
    }

    public Task PlaySuccessAsync(CancellationToken ct = default) =>
        PlayWavFileAsync(_options.SuccessSoundPath, ct);

    public Task PlayErrorAsync(CancellationToken ct = default) =>
        PlayWavFileAsync(_options.ErrorSoundPath, ct);

    public async Task PlaySpeechAsync(string text, CancellationToken ct = default)
    {
        if (_options.FeedbackProfile == "none") return;

        if (_options.FeedbackProfile == "tone")
        {
            await PlaySuccessAsync(ct);
            return;
        }

        // voice profile: synthesize and play
        var result = await _tts.SynthesizeAsync(text, ct);
        if (!result.Success || result.Value is null)
        {
            _logger.LogWarning("TTS synthesis failed: {ErrorCode}", result.Error?.Code);
            await PlaySuccessAsync(ct);
            return;
        }

        await PlayPcmBytesAsync(result.Value, ct);
    }

    private Task PlayWavFileAsync(string path, CancellationToken ct)
    {
        if (_options.FeedbackProfile == "none") return Task.CompletedTask;

        if (!File.Exists(path))
        {
            _logger.LogDebug("Audio file not found at {Path} — skipping playback", path);
            return Task.CompletedTask;
        }

        try
        {
            using var reader = new AudioFileReader(path);
            using var output = new WaveOutEvent();
            output.Init(reader);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing && !ct.IsCancellationRequested)
                Thread.Sleep(50);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not play audio file {Path}", path);
        }

        return Task.CompletedTask;
    }

    private Task PlayPcmBytesAsync(byte[] audioBytes, CancellationToken ct)
    {
        try
        {
            using var ms = new MemoryStream(audioBytes);
            using var reader = new RawSourceWaveStream(ms, new WaveFormat(16000, 16, 1));
            using var output = new WaveOutEvent();
            output.Init(reader);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing && !ct.IsCancellationRequested)
                Thread.Sleep(50);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not play synthesized audio");
        }
        return Task.CompletedTask;
    }
}
