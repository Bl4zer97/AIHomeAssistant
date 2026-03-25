using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Transcribes raw PCM audio to text.</summary>
public interface ISpeechToTextService
{
    Task<PipelineResult<string>> TranscribeAsync(AudioSegment audio, CancellationToken ct = default);
}
