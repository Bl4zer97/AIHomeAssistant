namespace AIHomeAssistant.Infrastructure.Options;

public class AzureOptions
{
    public string SpeechRegion  { get; set; } = string.Empty;
    public string SpeechKey     { get; set; } = string.Empty;
    public string OpenAiEndpoint    { get; set; } = string.Empty;
    public string OpenAiKey         { get; set; } = string.Empty;
    public string OpenAiDeployment  { get; set; } = "gpt-4";
    public string VisionEndpoint { get; set; } = string.Empty;
    public string VisionKey      { get; set; } = string.Empty;
    public string FaceEndpoint       { get; set; } = string.Empty;
    public string FaceKey            { get; set; } = string.Empty;
    public string FacePersonGroupId  { get; set; } = "household";
}
