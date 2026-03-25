using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Analyzes camera frames for presence detection and face recognition.</summary>
public interface IVisionService
{
    Task<PipelineResult<bool>> DetectPresenceAsync(byte[] jpegFrame, CancellationToken ct = default);
    Task<PipelineResult<string?>> IdentifyPersonAsync(byte[] jpegFrame, string personGroupId, CancellationToken ct = default);
}
