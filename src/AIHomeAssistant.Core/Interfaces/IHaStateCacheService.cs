using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

public interface IHaStateCacheService
{
    HaState? GetState(string entityId);
    IReadOnlyList<HaState> GetAllStates();
}
