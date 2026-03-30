using System.Threading.Channels;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using Vosk;

namespace AIHomeAssistant.Infrastructure.Audio;

/// <summary>
/// Wake-word detector using Vosk offline speech recognition.
/// Listens continuously via NAudio WaveInEvent, transcribes locally with Vosk,
/// and triggers when the configured wake word is heard.
/// After wake word detection, collects the subsequent command audio and writes
/// it to the pipeline channel for STT processing.
///
/// Setup:
///   1. dotnet add package VoskApi
///   2. Download model from https://alphacephei.com/vosk/models (vosk-model-small-en)
///   3. Set Audio:VoskModelPath to the extracted model directory
///   4. Set Audio:WakeWord to your desired trigger word (e.g. "jarvis")
/// </summary>
public sealed class VoskWakeWordDetector : IWakeWordDetector
{
    private readonly AudioOptions _options;
    private readonly ILogger<VoskWakeWordDetector> _logger;

    private const double SilenceRmsThreshold = 0.01;
    private const int SilenceFramesRequired = 20;
    private const int MaxCommandSeconds = 10;

    public VoskWakeWordDetector(
        IOptions<AudioOptions> options,
        ILogger<VoskWakeWordDetector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(ChannelWriter<AudioSegment> output, CancellationToken ct)
    {
        if (!IsConfigured())
        {
            await Task.Delay(Timeout.Infinite, ct);
            return;
        }

        Vosk.Vosk.SetLogLevel(-1); // Suppress Vosk internal logs

        using var model = new Model(_options.VoskModelPath);

        _logger.LogInformation(
            "Vosk wake-word detector started — listening for \"{WakeWord}\"",
            _options.WakeWord);

        while (!ct.IsCancellationRequested)
        {
            var segment = await ListenAndCaptureAsync(model, ct);
            if (segment is not null)
                await output.WriteAsync(segment, ct);
        }
    }

    private async Task<AudioSegment?> ListenAndCaptureAsync(Model model, CancellationToken ct)
    {
        var wakeWordDetectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandSamples = new List<short>();
        int silentFrames = 0;

        using var recognizer = new VoskRecognizer(model, _options.SampleRate);
        recognizer.SetMaxAlternatives(0);
        recognizer.SetWords(false);

        using var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(_options.SampleRate, 16, 1),
            BufferMilliseconds = 100
        };

        waveIn.DataAvailable += (_, e) =>
        {
            if (wakeWordDetectedTcs.Task.IsCompleted)
            {
                // Collect command frames after wake word
                var frame = new short[e.BytesRecorded / 2];
                Buffer.BlockCopy(e.Buffer, 0, frame, 0, e.BytesRecorded);
                commandSamples.AddRange(frame);

                var rms = CalculateRms(frame);
                silentFrames = rms < SilenceRmsThreshold ? silentFrames + 1 : 0;

                if (silentFrames >= SilenceFramesRequired)
                    commandDoneTcs.TrySetResult(true);

                return;
            }

            // Run Vosk recognition to detect wake word
            if (recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var json = recognizer.Result();
                if (ContainsWakeWord(json))
                {
                    _logger.LogInformation("Wake word \"{WakeWord}\" detected", _options.WakeWord);
                    wakeWordDetectedTcs.TrySetResult(true);
                }
            }
            else
            {
                var partial = recognizer.PartialResult();
                if (ContainsWakeWord(partial))
                {
                    _logger.LogInformation("Wake word \"{WakeWord}\" detected (partial)", _options.WakeWord);
                    wakeWordDetectedTcs.TrySetResult(true);
                }
            }
        };

        waveIn.StartRecording();

        // Wait for wake word
        try
        {
            await wakeWordDetectedTcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            waveIn.StopRecording();
            return null;
        }

        // Collect command audio until silence or timeout
        using var cmdCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cmdCts.CancelAfter(TimeSpan.FromSeconds(MaxCommandSeconds));

        try
        {
            await commandDoneTcs.Task.WaitAsync(cmdCts.Token);
        }
        catch (OperationCanceledException) { /* use what was captured */ }
        finally
        {
            waveIn.StopRecording();
        }

        return commandSamples.Count > 0
            ? new AudioSegment(commandSamples.ToArray(), _options.SampleRate)
            : null;
    }

    private bool ContainsWakeWord(string json) =>
        json.Contains(_options.WakeWord, StringComparison.OrdinalIgnoreCase);

    private bool IsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.VoskModelPath))
        {
            _logger.LogWarning(
                "Vosk model path not configured (Audio:VoskModelPath). " +
                "Wake-word detection is disabled.");
            return false;
        }

        if (!Directory.Exists(_options.VoskModelPath))
        {
            _logger.LogWarning(
                "Vosk model directory not found at {Path}. " +
                "Download from https://alphacephei.com/vosk/models",
                _options.VoskModelPath);
            return false;
        }

        return true;
    }

    private static double CalculateRms(short[] samples)
    {
        if (samples.Length == 0) return 0;
        double sumSq = samples.Sum(s => (double)s * s);
        return Math.Sqrt(sumSq / samples.Length) / short.MaxValue;
    }

    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
}
