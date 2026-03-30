namespace AIHomeAssistant.Infrastructure.Options;

public class AudioOptions
{
    /// <summary>ALSA device identifier for Linux audio capture (e.g. "default").</summary>
    public string AlsaDeviceId { get; set; } = "default";
    public int SampleRate { get; set; } = 16000;
    public int ChunkSize { get; set; } = 512;
    /// <summary>tone | voice | none</summary>
    public string FeedbackProfile { get; set; } = "tone";
    /// <summary>
    /// Path to the Vosk model directory.
    /// Download from https://alphacephei.com/vosk/models (e.g. vosk-model-small-it-0.22).
    /// </summary>
    public string VoskModelPath { get; set; } = string.Empty;
    /// <summary>Wake word to listen for (case-insensitive).</summary>
    public string WakeWord { get; set; } = "computer";
    public string SuccessSoundPath { get; set; } = "audio/sounds/success.wav";
    public string ErrorSoundPath { get; set; } = "audio/sounds/error.wav";
}
