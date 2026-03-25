using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIHomeAssistant.Infrastructure.HomeAssistant;

/// <summary>
/// Routes resolved intents to HA service calls or utility handlers.
/// </summary>
public class CommandRouter : ICommandRouter
{
    private readonly IHomeAssistantClient _ha;
    private readonly IShoppingListRepository _shoppingList;
    private readonly ILogger<CommandRouter> _logger;

    public CommandRouter(
        IHomeAssistantClient ha,
        IShoppingListRepository shoppingList,
        ILogger<CommandRouter> logger)
    {
        _ha = ha;
        _shoppingList = shoppingList;
        _logger = logger;
    }

    public async Task<PipelineResult<string?>> RouteAsync(IntentResult intent, CancellationToken ct = default)
    {
        _logger.LogInformation("Routing intent {Action} for entity {EntityId}", intent.Action, intent.EntityId);

        return intent.Action switch
        {
            // ─── Lights ────────────────────────────────────────────────────────
            "light.turn_on" => await CallHaAsync("light", "turn_on", BuildServicePayload(intent), ct),
            "light.turn_off" => await CallHaAsync("light", "turn_off", BuildServicePayload(intent), ct),
            "light.toggle" => await CallHaAsync("light", "toggle", BuildServicePayload(intent), ct),

            // ─── Climate ───────────────────────────────────────────────────────
            "climate.set_temperature" => await CallHaAsync("climate", "set_temperature", BuildServicePayload(intent), ct),
            "climate.set_hvac_mode" => await CallHaAsync("climate", "set_hvac_mode", BuildServicePayload(intent), ct),

            // ─── Shopping list ─────────────────────────────────────────────────
            "shopping_list.add" when intent.Parameters.TryGetValue("item", out var item) =>
                await HandleShoppingAddAsync(item?.ToString() ?? string.Empty, ct),
            "shopping_list.read" => await HandleShoppingReadAsync(ct),
            "shopping_list.clear" => await HandleShoppingClearAsync(ct),

            // ─── Generic HA service call ───────────────────────────────────────
            var action when action.Contains('.') && !action.StartsWith("shopping_list") =>
                await RouteGenericHaActionAsync(action, intent, ct),

            _ => new PipelineResult<string?>(false,
                Error: new PipelineError("UNKNOWN_ACTION", $"No handler for action: {intent.Action}"))
        };
    }

    private async Task<PipelineResult<string?>> CallHaAsync(
        string domain, string service, object payload, CancellationToken ct)
    {
        var result = await _ha.CallServiceAsync(domain, service, payload, ct);
        return result.Success
            ? new PipelineResult<string?>(true, null)
            : new PipelineResult<string?>(false, Error: result.Error);
    }

    private static object BuildServicePayload(IntentResult intent)
    {
        var payload = new Dictionary<string, object?>(intent.Parameters)
        {
            ["entity_id"] = intent.EntityId
        };
        return payload;
    }

    private async Task<PipelineResult<string?>> HandleShoppingAddAsync(string item, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item))
            return new PipelineResult<string?>(false,
                Error: new PipelineError("INVALID_PARAMETER", "Shopping list item cannot be empty"));

        await _shoppingList.AddItemAsync(item, ct);
        return new PipelineResult<string?>(true, $"Ho aggiunto {item} alla lista");
    }

    private async Task<PipelineResult<string?>> HandleShoppingReadAsync(CancellationToken ct)
    {
        var items = await _shoppingList.GetAllItemsAsync(ct);
        if (items.Count == 0)
            return new PipelineResult<string?>(true, "La lista è vuota");

        var response = "In lista hai: " + string.Join(", ", items);
        return new PipelineResult<string?>(true, response);
    }

    private async Task<PipelineResult<string?>> HandleShoppingClearAsync(CancellationToken ct)
    {
        await _shoppingList.ClearAllAsync(ct);
        return new PipelineResult<string?>(true, "Lista della spesa svuotata");
    }

    private async Task<PipelineResult<string?>> RouteGenericHaActionAsync(
        string action, IntentResult intent, CancellationToken ct)
    {
        var parts = action.Split('.', 2);
        var result = await _ha.CallServiceAsync(parts[0], parts[1], BuildServicePayload(intent), ct);
        return result.Success
            ? new PipelineResult<string?>(true, null)
            : new PipelineResult<string?>(false, Error: result.Error);
    }
}
