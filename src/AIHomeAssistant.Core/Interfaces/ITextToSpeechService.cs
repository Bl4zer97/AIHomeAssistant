using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Synthesizes text to speech audio.</summary>
public interface ITextToSpeechService
{
    Task<PipelineResult<byte[]>> SynthesizeAsync(string text, CancellationToken ct = default);
}
