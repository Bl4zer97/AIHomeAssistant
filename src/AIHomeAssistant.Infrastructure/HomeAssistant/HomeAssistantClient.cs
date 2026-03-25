using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIHomeAssistant.Infrastructure.HomeAssistant;

public class HomeAssistantClient : IHomeAssistantClient
{
    // ─── Service call methods (Story 1.6) ──────────────────────────────────────

    /// <summary>Calls a HA service action (e.g., light.turn_on).</summary>
    public async Task<PipelineResult> CallServiceAsync(
        string domain,
        string service,
        object payload,
        CancellationToken ct = default)
    {
        try
        {
            var content = JsonContent.Create(payload);
            var response = await _httpClient.PostAsync($"api/services/{domain}/{service}", content, ct);

            _logger.LogInformation(
                "HA service call {Domain}.{Service} → HTTP {StatusCode}", domain, service, (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("HA service call failed {Domain}.{Service}: HTTP {Status} — {Body}",
                    domain, service, (int)response.StatusCode, body[..Math.Min(200, body.Length)]);
                return new PipelineResult(false,
                    new PipelineError("HA_CALL_FAILED", $"HA returned HTTP {(int)response.StatusCode}"));
            }

            return new PipelineResult(true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HA service call network error {Domain}.{Service}: {Code}",
                domain, service, "HA_UNAVAILABLE");
            return new PipelineResult(false,
                new PipelineError("HA_UNAVAILABLE", $"Network error calling {domain}.{service}", ex));
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            _logger.LogError(ex, "HA service call timed out {Domain}.{Service}: {Code}",
                domain, service, "HA_UNAVAILABLE");
            return new PipelineResult(false,
                new PipelineError("HA_UNAVAILABLE", $"Timeout calling {domain}.{service}", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HA service call unexpected error {Domain}.{Service}: {Code}",
                domain, service, "HA_UNAVAILABLE");
            return new PipelineResult(false,
                new PipelineError("HA_UNAVAILABLE", $"Unexpected error calling {domain}.{service}", ex));
        }
    }


    private readonly HttpClient _httpClient;
    private readonly ILogger<HomeAssistantClient> _logger;

    public HomeAssistantClient(HttpClient httpClient, ILogger<HomeAssistantClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PipelineResult<HaState>> GetStateAsync(string entityId, CancellationToken ct = default)
    {
        try
        {
            var dto = await _httpClient.GetFromJsonAsync<HaStateDto>(
                $"api/states/{entityId}", ct);

            if (dto is null)
            {
                _logger.LogWarning("HA API returned null for entity {EntityId}", entityId);
                return new PipelineResult<HaState>(false, Error: new PipelineError(
                    "HA_DESERIALIZATION_ERROR", $"Null response for entity {entityId}"));
            }

            return new PipelineResult<HaState>(true, dto.ToHaState());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HA API unavailable fetching state for {EntityId}: {Code}",
                entityId, "HA_UNAVAILABLE");
            return new PipelineResult<HaState>(false, Error: new PipelineError(
                "HA_UNAVAILABLE", $"HTTP error fetching state for {entityId}", ex));
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            _logger.LogError(ex, "HA API request timed out for {EntityId}: {Code}",
                entityId, "HA_UNAVAILABLE");
            return new PipelineResult<HaState>(false, Error: new PipelineError(
                "HA_UNAVAILABLE", $"Request timed out for entity {entityId}", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching HA state for {EntityId}: {Code}",
                entityId, "HA_UNAVAILABLE");
            return new PipelineResult<HaState>(false, Error: new PipelineError(
                "HA_UNAVAILABLE", $"Unexpected error for entity {entityId}", ex));
        }
    }

    public async Task<PipelineResult<IReadOnlyList<HaState>>> GetAllStatesAsync(CancellationToken ct = default)
    {
        try
        {
            var dtos = await _httpClient.GetFromJsonAsync<List<HaStateDto>>("api/states", ct);

            if (dtos is null)
            {
                _logger.LogWarning("HA API returned null for all-states request");
                return new PipelineResult<IReadOnlyList<HaState>>(false, Error: new PipelineError(
                    "HA_DESERIALIZATION_ERROR", "Null response from /api/states"));
            }

            IReadOnlyList<HaState> states = dtos.Select(d => d.ToHaState()).ToList();
            return new PipelineResult<IReadOnlyList<HaState>>(true, states);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HA API unavailable fetching all states: {Code}", "HA_UNAVAILABLE");
            return new PipelineResult<IReadOnlyList<HaState>>(false, Error: new PipelineError(
                "HA_UNAVAILABLE", "HTTP error fetching all states", ex));
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            _logger.LogError(ex, "HA API request timed out for all states: {Code}", "HA_UNAVAILABLE");
            return new PipelineResult<IReadOnlyList<HaState>>(false, Error: new PipelineError(
                "HA_UNAVAILABLE", "Request timed out fetching all states", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching all HA states: {Code}", "HA_UNAVAILABLE");
            return new PipelineResult<IReadOnlyList<HaState>>(false, Error: new PipelineError(
                "HA_UNAVAILABLE", "Unexpected error fetching all states", ex));
        }
    }

    // Private DTO matching the HA REST API JSON shape
    private sealed class HaStateDto
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("attributes")]
        public Dictionary<string, object?> Attributes { get; set; } = [];

        [JsonPropertyName("last_updated")]
        public DateTimeOffset LastUpdated { get; set; }

        public HaState ToHaState() => new(EntityId, State, Attributes, LastUpdated);
    }
}
