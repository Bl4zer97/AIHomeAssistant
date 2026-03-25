using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHomeAssistant.Infrastructure.Azure;

/// <summary>
/// Azure Speech-to-Text service using the Azure Cognitive Services Speech SDK.
/// Transcribes 16kHz mono PCM audio segments to Italian text.
/// </summary>
public class AzureSpeechToTextService : ISpeechToTextService
{
    private readonly AzureOptions _options;
    private readonly ILogger<AzureSpeechToTextService> _logger;

    public AzureSpeechToTextService(IOptions<AzureOptions> options, ILogger<AzureSpeechToTextService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PipelineResult<string>> TranscribeAsync(AudioSegment audio, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SpeechKey) || string.IsNullOrWhiteSpace(_options.SpeechRegion))
        {
            _logger.LogWarning("Azure Speech credentials not configured");
            return new PipelineResult<string>(false,
                Error: new PipelineError("STT_SERVICE_UNAVAILABLE", "Azure Speech credentials not configured"));
        }

        try
        {
            var config = SpeechConfig.FromSubscription(_options.SpeechKey, _options.SpeechRegion);
            config.SpeechRecognitionLanguage = "it-IT";

            // Convert Int16 sample array to byte array for PushAudioInputStream
            var pcmBytes = new byte[audio.Samples.Length * 2];
            Buffer.BlockCopy(audio.Samples, 0, pcmBytes, 0, pcmBytes.Length);

            var audioFormat = AudioStreamFormat.GetWaveFormatPCM((uint)audio.SampleRate, 16, 1);
            using var pushStream = AudioInputStream.CreatePushStream(audioFormat);
            pushStream.Write(pcmBytes);
            pushStream.Close();

            using var audioConfig = AudioConfig.FromStreamInput(pushStream);
            using var recognizer = new SpeechRecognizer(config, audioConfig);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await recognizer.RecognizeOnceAsync();

            return result.Reason switch
            {
                ResultReason.RecognizedSpeech when !string.IsNullOrWhiteSpace(result.Text)
                    => new PipelineResult<string>(true, result.Text),
                ResultReason.NoMatch
                    => new PipelineResult<string>(false, Error: new PipelineError(
                        "STT_LOW_CONFIDENCE", $"No speech recognized. NoMatch reason: {NoMatchDetails.FromResult(result).Reason}")),
                ResultReason.RecognizedSpeech
                    => new PipelineResult<string>(false, Error: new PipelineError(
                        "STT_LOW_CONFIDENCE", "Empty transcription result")),
                ResultReason.Canceled => HandleCancellation(result),
                _ => new PipelineResult<string>(false, Error: new PipelineError(
                    "STT_SERVICE_UNAVAILABLE", $"Unhandled result reason: {result.Reason}"))
            };
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            _logger.LogError(ex, "Azure Speech STT timed out: {Code}", "STT_SERVICE_UNAVAILABLE");
            return new PipelineResult<string>(false,
                Error: new PipelineError("STT_SERVICE_UNAVAILABLE", "Request timed out", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Speech STT failed: {Code}", "STT_SERVICE_UNAVAILABLE");
            return new PipelineResult<string>(false,
                Error: new PipelineError("STT_SERVICE_UNAVAILABLE", "Unexpected STT error", ex));
        }
    }

    private PipelineResult<string> HandleCancellation(SpeechRecognitionResult result)
    {
        var cancellation = CancellationDetails.FromResult(result);
        if (cancellation.Reason == CancellationReason.Error)
        {
            _logger.LogError("Azure Speech STT cancelled with error {ErrorCode}: {ErrorDetails}",
                cancellation.ErrorCode, cancellation.ErrorDetails);
            return new PipelineResult<string>(false,
                Error: new PipelineError("STT_SERVICE_UNAVAILABLE",
                    $"Azure Speech error {cancellation.ErrorCode}: {cancellation.ErrorDetails}"));
        }
        return new PipelineResult<string>(false,
            Error: new PipelineError("STT_LOW_CONFIDENCE", "Speech recognition cancelled"));
    }
}
