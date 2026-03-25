using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Text.Json;

namespace AIHomeAssistant.Infrastructure.Azure;

/// <summary>
/// Azure OpenAI GPT-4 intent resolution service.
/// Uses Chat Completions with JSON mode to resolve Italian natural language to structured IntentResult.
/// </summary>
public class AzureOpenAiIntentService : IIntentResolutionService
{
    private readonly AzureOptions _options;
    private readonly ILogger<AzureOpenAiIntentService> _logger;

    private static readonly string SystemPrompt = """
        You are an Italian home automation assistant. 
        Resolve the user's Italian utterance to a structured JSON intent.
        
        Available actions:
        - light.turn_on / light.turn_off / light.toggle (entity_id, optional brightness 0-255)
        - climate.set_temperature (entity_id, temperature float, optional hvac_mode: heat|cool|auto|off)
        - shopping_list.add (item: string)
        - shopping_list.read
        - shopping_list.clear
        - alert.clear
        
        Respond ONLY with valid JSON matching this schema:
        {
          "intent_type": "Action" | "Query",
          "entity_id": "string (HA entity id, e.g. light.living_room) or empty for utility actions",
          "action": "string (e.g. light.turn_on)",
          "parameters": { "key": "value" }
        }
        
        For comfort-sensation utterances like "ho caldo"/"ho freddo", 
        use climate.set_temperature with a contextually appropriate temperature.
        Current HA states are provided in the user message as context.
        Never hardcode entity IDs — always use the provided entity IDs from context.
        """;

    public AzureOpenAiIntentService(IOptions<AzureOptions> options, ILogger<AzureOpenAiIntentService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PipelineResult<IntentResult>> ResolveIntentAsync(
        string transcript,
        IReadOnlyList<HaState> haContext,
        IReadOnlyList<string>? sessionHistory = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.OpenAiEndpoint) || string.IsNullOrWhiteSpace(_options.OpenAiKey))
        {
            _logger.LogWarning("Azure OpenAI credentials not configured");
            return new PipelineResult<IntentResult>(false,
                Error: new PipelineError("INTENT_SERVICE_UNAVAILABLE", "Azure OpenAI credentials not configured"));
        }

        try
        {
            var client = new AzureOpenAIClient(new Uri(_options.OpenAiEndpoint),
                new AzureKeyCredential(_options.OpenAiKey));
            var chatClient = client.GetChatClient(_options.OpenAiDeployment);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt)
            };

            // Add session history
            if (sessionHistory?.Count > 0)
                messages.Add(new UserChatMessage($"Previous commands: {string.Join("; ", sessionHistory)}"));

            // Build context message with HA states
            var contextSummary = string.Join("\n", haContext.Take(20).Select(s =>
                $"{s.EntityId}: {s.State}"));

            messages.Add(new UserChatMessage(
                $"Current entity states:\n{contextSummary}\n\nUser said: {transcript}"));

            var completionOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var completion = await chatClient.CompleteChatAsync(messages, completionOptions, ct);
            var json = completion.Value.Content[0].Text;

            _logger.LogDebug("OpenAI raw response (truncated): {Json}", json[..Math.Min(200, json.Length)]);

            return ParseIntentResult(json);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            _logger.LogError(ex, "Azure OpenAI request timed out: {Code}", "INTENT_SERVICE_UNAVAILABLE");
            return new PipelineResult<IntentResult>(false,
                Error: new PipelineError("INTENT_SERVICE_UNAVAILABLE", "Request timed out", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI intent resolution failed: {Code}", "INTENT_SERVICE_UNAVAILABLE");
            return new PipelineResult<IntentResult>(false,
                Error: new PipelineError("INTENT_SERVICE_UNAVAILABLE", "Unexpected error", ex));
        }
    }

    private PipelineResult<IntentResult> ParseIntentResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var intentTypeStr = root.GetProperty("intent_type").GetString() ?? "Action";
            var intentType = intentTypeStr == "Query" ? IntentType.Query : IntentType.Action;
            var entityId = root.TryGetProperty("entity_id", out var eid) ? eid.GetString() ?? string.Empty : string.Empty;
            var action = root.TryGetProperty("action", out var act) ? act.GetString() ?? string.Empty : string.Empty;

            var parameters = new Dictionary<string, object?>();
            if (root.TryGetProperty("parameters", out var paramsEl))
            {
                foreach (var p in paramsEl.EnumerateObject())
                {
                    parameters[p.Name] = p.Value.ValueKind switch
                    {
                        JsonValueKind.Number => p.Value.TryGetDouble(out var d) ? (object?)d : p.Value.GetRawText(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => p.Value.GetString()
                    };
                }
            }

            return new PipelineResult<IntentResult>(true,
                new IntentResult(intentType, entityId, action, parameters));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI response as IntentResult: {Code}", "INTENT_RESOLUTION_FAILED");
            return new PipelineResult<IntentResult>(false,
                Error: new PipelineError("INTENT_RESOLUTION_FAILED", "Could not parse intent JSON", ex));
        }
    }
}
