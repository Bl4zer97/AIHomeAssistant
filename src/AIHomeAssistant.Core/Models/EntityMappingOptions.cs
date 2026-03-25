namespace AIHomeAssistant.Core.Models;

/// <summary>
/// Strongly-typed options bound from config/entities.json.
/// Schema: { "lights": { "salotto": "light.living_room" }, "climate": { "salotto": "climate.living_room" } }
/// </summary>
public class EntityMappingOptions
{
    public Dictionary<string, string> Lights { get; set; } = [];
    public Dictionary<string, string> Climate { get; set; } = [];
}
