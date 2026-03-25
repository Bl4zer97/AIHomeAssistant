using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHomeAssistant.Infrastructure.Azure;

/// <summary>
/// Azure Speech Text-to-Speech service.
/// Synthesizes Italian text to 16kHz mono PCM audio bytes.
/// </summary>
public class AzureTextToSpeechService : ITextToSpeechService
{
    private readonly AzureOptions _options;
    private readonly ILogger<AzureTextToSpeechService> _logger;

    public AzureTextToSpeechService(IOptions<AzureOptions> options, ILogger<AzureTextToSpeechService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PipelineResult<byte[]>> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SpeechKey) || string.IsNullOrWhiteSpace(_options.SpeechRegion))
        {
            _logger.LogWarning("Azure Speech credentials not configured");
            return new PipelineResult<byte[]>(false,
                Error: new PipelineError("TTS_SERVICE_UNAVAILABLE", "Azure Speech credentials not configured"));
        }

        try
        {
            var config = SpeechConfig.FromSubscription(_options.SpeechKey, _options.SpeechRegion);
            config.SpeechSynthesisLanguage = "it-IT";
            config.SpeechSynthesisVoiceName = "it-IT-ElsaNeural";
            config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm);

            using var synthesizer = new SpeechSynthesizer(config, null);
            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                return new PipelineResult<byte[]>(true, result.AudioData);

            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            _logger.LogError("TTS synthesis failed: {ErrorCode} — {Details}",
                cancellation.ErrorCode, cancellation.ErrorDetails);
            return new PipelineResult<byte[]>(false,
                Error: new PipelineError("TTS_SERVICE_UNAVAILABLE",
                    $"TTS failed: {cancellation.ErrorCode} — {cancellation.ErrorDetails}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure TTS synthesis error: {Code}", "TTS_SERVICE_UNAVAILABLE");
            return new PipelineResult<byte[]>(false,
                Error: new PipelineError("TTS_SERVICE_UNAVAILABLE", "Unexpected TTS error", ex));
        }
    }
}
