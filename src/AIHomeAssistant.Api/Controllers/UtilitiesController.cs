using AIHomeAssistant.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIHomeAssistant.Api.Controllers;

/// <summary>
/// Utility endpoints: shopping list management (Stories 2.1-2.2).
/// GET  /api/utilities/shopping-list        — list all active items
/// POST /api/utilities/shopping-list        — add an item
/// DELETE /api/utilities/shopping-list      — clear all items
/// </summary>
[ApiController]
[Route("api/[controller]/shopping-list")]
public class UtilitiesController : ControllerBase
{
    private readonly IShoppingListRepository _repo;
    private readonly ILogger<UtilitiesController> _logger;

    public UtilitiesController(IShoppingListRepository repo, ILogger<UtilitiesController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(CancellationToken ct)
    {
        var items = await _repo.GetAllItemsAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddItem([FromBody] AddItemRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Item))
            return UnprocessableEntity(new ProblemDetails { Title = "Item is required" });

        await _repo.AddItemAsync(req.Item.Trim(), ct);
        _logger.LogInformation("Shopping list item added: {Item}", req.Item);
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearItems(CancellationToken ct)
    {
        await _repo.ClearAllAsync(ct);
        _logger.LogInformation("Shopping list cleared");
        return NoContent();
    }
}

public record AddItemRequest(string Item);
