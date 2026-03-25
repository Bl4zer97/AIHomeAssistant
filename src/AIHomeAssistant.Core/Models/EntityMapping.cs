namespace AIHomeAssistant.Core.Models;

/// <summary>
/// Domain helper for resolving HA entity IDs from friendly names via the entities.json mapping.
/// Always pass the latest <see cref="EntityMappingOptions"/> snapshot (from IOptionsMonitor).
/// </summary>
public static class EntityMapping
{
    /// <summary>
    /// Attempts to resolve a HA entity ID from a domain and friendly name.
    /// </summary>
    /// <param name="options">Current entity mapping options snapshot.</param>
    /// <param name="domain">Domain key, e.g. "lights" or "climate".</param>
    /// <param name="friendlyName">Friendly name key, e.g. "salotto".</param>
    /// <param name="entityId">Resolved HA entity ID, e.g. "light.living_room".</param>
    /// <returns>True if resolved successfully; false if domain or friendly name not found.</returns>
    public static bool TryResolveEntityId(
        EntityMappingOptions options,
        string domain,
        string friendlyName,
        out string entityId)
    {
        entityId = string.Empty;
        return domain switch
        {
            "lights" => options.Lights.TryGetValue(friendlyName, out entityId!),
            "climate" => options.Climate.TryGetValue(friendlyName, out entityId!),
            _ => false
        };
    }
}
