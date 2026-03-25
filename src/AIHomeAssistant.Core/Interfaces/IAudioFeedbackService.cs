namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Plays audio feedback (tone or TTS response).</summary>
public interface IAudioFeedbackService
{
    Task PlaySuccessAsync(CancellationToken ct = default);
    Task PlayErrorAsync(CancellationToken ct = default);
    Task PlaySpeechAsync(string text, CancellationToken ct = default);
}
