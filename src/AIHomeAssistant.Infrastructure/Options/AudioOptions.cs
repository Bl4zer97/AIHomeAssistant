namespace AIHomeAssistant.Infrastructure.Options;

public class AudioOptions
{
    /// <summary>ALSA device identifier for Linux audio capture (e.g. "default").</summary>
    public string AlsaDeviceId { get; set; } = "default";
    public int SampleRate { get; set; } = 16000;
    public int ChunkSize { get; set; } = 512;
    /// <summary>tone | voice | none</summary>
    public string FeedbackProfile { get; set; } = "tone";
    public string PorcupineAccessKey { get; set; } = string.Empty;
    public string SuccessSoundPath { get; set; } = "audio/sounds/success.wav";
    public string ErrorSoundPath { get; set; } = "audio/sounds/error.wav";
}
