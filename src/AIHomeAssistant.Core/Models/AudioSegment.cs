namespace AIHomeAssistant.Core.Models;

/// <summary>
/// A captured audio segment emitted by the wake-word/capture pipeline.
/// Contains raw 16kHz mono PCM samples as Int16 array.
/// </summary>
public record AudioSegment(short[] Samples, int SampleRate = 16000);
