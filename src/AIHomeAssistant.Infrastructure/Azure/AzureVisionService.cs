using System.Net.Http.Headers;
using System.Text.Json;
using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHomeAssistant.Infrastructure.Azure;

/// <summary>
/// Implements IVisionService using Azure AI Vision (ImageAnalysis) for presence detection
/// and Azure Face REST API v1.2 for person identification.
/// </summary>
public sealed class AzureVisionService : IVisionService
{
    private readonly AzureOptions _options;
    private readonly HttpClient _faceHttpClient;
    private readonly ILogger<AzureVisionService> _logger;

    public AzureVisionService(
        IOptions<AzureOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AzureVisionService> logger)
    {
        _options = options.Value;
        _faceHttpClient = httpClientFactory.CreateClient("AzureFace");
        _logger = logger;
    }

    /// <summary>
    /// Returns true when at least one person is detected in the image.
    /// Uses Azure AI Vision ImageAnalysis People feature.
    /// </summary>
    public async Task<PipelineResult<bool>> DetectPresenceAsync(byte[] imageData, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.VisionEndpoint) || string.IsNullOrWhiteSpace(_options.VisionKey))
        {
            _logger.LogWarning("Azure Vision not configured");
            return new PipelineResult<bool>(false, false,
                new PipelineError("VISION_NOT_CONFIGURED", "Azure Vision endpoint/key not set"));
        }

        try
        {
            var client = new ImageAnalysisClient(
                new Uri(_options.VisionEndpoint),
                new AzureKeyCredential(_options.VisionKey));

            using var stream = new MemoryStream(imageData);
            var result = await client.AnalyzeAsync(
                BinaryData.FromStream(stream),
                VisualFeatures.People,
                cancellationToken: ct);

            var anyPerson = result.Value.People?.Values?.Any(p => p.Confidence >= 0.5) ?? false;
            _logger.LogDebug("Presence detection: {Detected}", anyPerson);
            return new PipelineResult<bool>(true, anyPerson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Vision presence detection failed");
            return new PipelineResult<bool>(false, false,
                new PipelineError("VISION_DETECTION_FAILED", "Presence detection failed", ex));
        }
    }

    /// <summary>
    /// Identifies a person from an image using Azure Face REST API v1.0.
    /// Returns the PersonName if identified, or null if no match.
    /// Falls back to options PersonGroupId if not explicitly provided.
    /// </summary>
    public async Task<PipelineResult<string?>> IdentifyPersonAsync(
        byte[] imageData,
        string personGroupId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FaceEndpoint) || string.IsNullOrWhiteSpace(_options.FaceKey))
        {
            _logger.LogWarning("Azure Face not configured");
            return new PipelineResult<string?>(false, null,
                new PipelineError("FACE_NOT_CONFIGURED", "Azure Face endpoint/key not set"));
        }

        // Use provided personGroupId, fall back to configured default
        var groupId = string.IsNullOrWhiteSpace(personGroupId)
            ? _options.FacePersonGroupId
            : personGroupId;

        try
        {
            // Step 1: Detect faces
            var detectUrl = $"{_options.FaceEndpoint.TrimEnd('/')}/face/v1.0/detect?returnFaceId=true&returnFaceLandmarks=false";
            using var imgContent = new ByteArrayContent(imageData);
            imgContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            _faceHttpClient.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
            _faceHttpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _options.FaceKey);

            var detectResponse = await _faceHttpClient.PostAsync(detectUrl, imgContent, ct);
            if (!detectResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Face detect failed: {Status}", detectResponse.StatusCode);
                return new PipelineResult<string?>(false, null,
                    new PipelineError("FACE_DETECT_FAILED", $"HTTP {(int)detectResponse.StatusCode}"));
            }

            var detectBody = await detectResponse.Content.ReadAsStringAsync(ct);
            using var detectDoc = JsonDocument.Parse(detectBody);
            var faces = detectDoc.RootElement.EnumerateArray().ToList();
            if (faces.Count == 0)
            {
                _logger.LogDebug("No faces detected in image");
                return new PipelineResult<string?>(true, null);
            }

            var faceId = faces[0].GetProperty("faceId").GetString()!;

            // Step 2: Identify face
            var identifyUrl = $"{_options.FaceEndpoint.TrimEnd('/')}/face/v1.0/identify";
            var identifyPayload = JsonSerializer.Serialize(new
            {
                personGroupId = groupId,
                faceIds = new[] { faceId },
                maxNumOfCandidatesReturned = 1,
                confidenceThreshold = 0.6
            });
            using var idContent = new StringContent(identifyPayload, System.Text.Encoding.UTF8, "application/json");
            var identifyResponse = await _faceHttpClient.PostAsync(identifyUrl, idContent, ct);

            if (!identifyResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Face identify failed: {Status}", identifyResponse.StatusCode);
                return new PipelineResult<string?>(false, null,
                    new PipelineError("FACE_IDENTIFY_FAILED", $"HTTP {(int)identifyResponse.StatusCode}"));
            }

            var idBody = await identifyResponse.Content.ReadAsStringAsync(ct);
            using var idDoc = JsonDocument.Parse(idBody);
            var results = idDoc.RootElement.EnumerateArray().FirstOrDefault();
            var candidates = results.TryGetProperty("candidates", out var c) ? c.EnumerateArray().ToList() : [];

            if (candidates.Count == 0)
            {
                _logger.LogDebug("No matching person found");
                return new PipelineResult<string?>(true, null);
            }

            var personId = candidates[0].GetProperty("personId").GetString()!;

            // Step 3: Fetch person name
            var personUrl = $"{_options.FaceEndpoint.TrimEnd('/')}/face/v1.0/persongroups/{groupId}/persons/{personId}";
            var personResponse = await _faceHttpClient.GetAsync(personUrl, ct);
            if (!personResponse.IsSuccessStatusCode)
                return new PipelineResult<string?>(true, personId); // return ID if name fetch fails

            var personBody = await personResponse.Content.ReadAsStringAsync(ct);
            using var personDoc = JsonDocument.Parse(personBody);
            var name = personDoc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
            _logger.LogInformation("Person identified: {Name}", name ?? personId);
            return new PipelineResult<string?>(true, name ?? personId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Face identification failed");
            return new PipelineResult<string?>(false, null,
                new PipelineError("FACE_IDENTIFY_FAILED", "Face identification failed", ex));
        }
    }
}
