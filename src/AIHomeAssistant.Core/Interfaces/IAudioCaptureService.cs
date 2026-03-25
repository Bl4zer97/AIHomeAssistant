using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Captures PCM audio from the microphone after wake-word detection.</summary>
public interface IAudioCaptureService
{
    /// <summary>Captures audio until silence and returns the captured PCM segment.</summary>
    Task<PipelineResult<AudioSegment>> CaptureUtteranceAsync(CancellationToken ct = default);
}
