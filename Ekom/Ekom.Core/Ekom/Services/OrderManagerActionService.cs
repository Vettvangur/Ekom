using Ekom.Models;
using Ekom.Models.Manager;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

public sealed class OrderManagerActionService : IOrderManagerActionService
{
    private readonly IEnumerable<IOrderManagerActionProvider> _providers;
    private readonly ILogger<OrderManagerActionService> _logger;

    public OrderManagerActionService(
        IEnumerable<IOrderManagerActionProvider> providers,
        ILogger<OrderManagerActionService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(IOrderInfo orderInfo, CancellationToken ct = default)
    {
        List<OrderManagerAction> actions = new();
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);

        foreach (IOrderManagerActionProvider provider in _providers)
        {
            IReadOnlyCollection<OrderManagerAction> providerActions = await provider.GetActionsAsync(orderInfo, ct).ConfigureAwait(false);

            foreach (OrderManagerAction action in providerActions)
            {
                if (string.IsNullOrWhiteSpace(action.Key))
                {
                    continue;
                }

                if (!keys.Add(action.Key))
                {
                    _logger.LogWarning("Duplicate order manager action key {ActionKey} ignored for order {OrderId}", action.Key, orderInfo.UniqueId);
                    continue;
                }

                actions.Add(action);
            }
        }

        return actions
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<OrderManagerActionExecutionResult?> ExecuteAsync(IOrderInfo orderInfo, string actionKey, string? userName = null, CancellationToken ct = default)
    {
        foreach (IOrderManagerActionProvider provider in _providers)
        {
            OrderManagerActionExecutionResult? result = await provider.ExecuteAsync(orderInfo, actionKey, userName, ct).ConfigureAwait(false);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
