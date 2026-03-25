using System.Threading.Channels;
using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>
/// Listens continuously for a wake-word and writes detected AudioSegments to the provided channel.
/// </summary>
public interface IWakeWordDetector : IDisposable
{
    /// <summary>Starts listening. Detected audio segments are written to <paramref name="output"/>.</summary>
    Task StartAsync(ChannelWriter<AudioSegment> output, CancellationToken ct);
    Task StopAsync();
}
