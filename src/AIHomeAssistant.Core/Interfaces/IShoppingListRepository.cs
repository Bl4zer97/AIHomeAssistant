using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Shopping list CRUD operations.</summary>
public interface IShoppingListRepository
{
    Task AddItemAsync(string item, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllItemsAsync(CancellationToken ct = default);
    Task ClearAllAsync(CancellationToken ct = default);
}
