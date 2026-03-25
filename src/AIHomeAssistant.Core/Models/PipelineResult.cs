namespace AIHomeAssistant.Core.Models;

public record PipelineError(string Code, string Message, Exception? Exception = null);

public record PipelineResult(bool Success, PipelineError? Error = null);

public record PipelineResult<T>(bool Success, T? Value = default, PipelineError? Error = null);
