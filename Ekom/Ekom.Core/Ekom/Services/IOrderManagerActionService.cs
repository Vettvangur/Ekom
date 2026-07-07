using Ekom.Models;
using Ekom.Models.Manager;

namespace Ekom.Services;

public interface IOrderManagerActionService
{
    Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(IOrderInfo orderInfo, CancellationToken ct = default);

    Task<OrderManagerActionExecutionResult?> ExecuteAsync(IOrderInfo orderInfo, string actionKey, string? userName = null, CancellationToken ct = default);
}
